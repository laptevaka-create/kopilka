using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контрактные тесты EF-слоя хранилища: WAL после последовательности
/// старта приложения, ограничение внешних ключей Restrict на уровне
/// самой базы (raw-DELETE мимо репозиториев), сохранность сидовых
/// записей при ResetToSeed, маппинг DateOnly на границах календаря,
/// конвертер CategoryKind в обе стороны и явная загрузка навигаций.
/// </summary>
public class EfStorageContractTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public EfStorageContractTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    // Сид миграции InitialCreate: счета 1 «Наличные», 2 «Карта»;
    // категории 1 «Зарплата» (доход), 3 «Продукты» (расход).

    [Fact]
    public void Wal_EnabledBeforeMigrations_PersistsInFileAndSurvivesEfConnections()
    {
        // Последовательность старта приложения (App.xaml.cs): сначала
        // raw-PRAGMA journal_mode = WAL, затем миграции EF Core.
        string directory = Path.Combine(Path.GetTempPath(), "kopilka-tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(directory, "wal-test.db");
        try
        {
            Directory.CreateDirectory(directory);
            ExecuteRaw(databasePath, "PRAGMA journal_mode = WAL;");

            DbContextOptions<KopilkaDbContext> options = new DbContextOptionsBuilder<KopilkaDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            using (var context = new KopilkaDbContext(options))
            {
                context.Database.Migrate();
            }

            Assert.Equal("wal", ReadScalar(databasePath, "PRAGMA journal_mode;"));

            // Режим хранится в файле базы: соединения EF его не сбрасывают.
            using (var context = new KopilkaDbContext(options))
            {
                Assert.Equal(2, context.Accounts.Count());
            }

            Assert.Equal("wal", ReadScalar(databasePath, "PRAGMA journal_mode;"));
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // -wal/-shm могут ещё удерживаться драйвером; папка во
                // временных файлах, системе не мешает.
            }
        }
    }

    [Fact]
    public void RawDelete_AccountWithTransactions_IsBlockedByDatabaseForeignKey()
    {
        var repository = new EfTransactionRepository(_fixture.ContextFactory);
        repository.Add(accountId: 1, categoryId: 3, amountKopecks: 100, date: new DateOnly(2026, 3, 5), comment: null);

        // Голый DELETE мимо репозитория: база сама запрещает удалять
        // используемый счёт — Restrict, а не только логика DeleteResult.
        Assert.Throws<SqliteException>(() =>
            ExecuteRaw(_fixture.DatabasePath, "DELETE FROM accounts WHERE id = 1;"));
    }

    [Fact]
    public void RawDelete_CategoryWithTransactions_IsBlockedByDatabaseForeignKey()
    {
        var repository = new EfTransactionRepository(_fixture.ContextFactory);
        repository.Add(accountId: 1, categoryId: 3, amountKopecks: 100, date: new DateOnly(2026, 3, 5), comment: null);

        Assert.Throws<SqliteException>(() =>
            ExecuteRaw(_fixture.DatabasePath, "DELETE FROM categories WHERE id = 3;"));
    }

    [Fact]
    public void RawDelete_CategoryWithBudget_IsBlockedByDatabaseForeignKey()
    {
        // Бюджет — тоже ссылка Restrict на категорию: таблица наполняется
        // с шага 4, но защита базы работает уже сегодня.
        Budget budget;
        using (KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            budget = new Budget { CategoryId = 3, Month = "2026-03", LimitKopecks = 10_000 };
            context.Budgets.Add(budget);
            context.SaveChanges();
        }

        try
        {
            Assert.Throws<SqliteException>(() =>
                ExecuteRaw(_fixture.DatabasePath, "DELETE FROM categories WHERE id = 3;"));
        }
        finally
        {
            // Чистим за собой: ResetToSeed бюджеты не удаляет.
            using KopilkaDbContext cleanup = _fixture.ContextFactory.CreateDbContext();
            Budget? created = cleanup.Budgets.Find(budget.Id);
            if (created is not null)
            {
                cleanup.Budgets.Remove(created);
                cleanup.SaveChanges();
            }
        }
    }

    [Fact]
    public void ResetToSeed_RemovesUserData_KeepsSeedRowsWithoutDuplicates()
    {
        var accounts = new EfAccountRepository(_fixture.ContextFactory);
        long customAccountId = accounts.Add("Временный счёт", sortOrder: 9);
        var transactions = new EfTransactionRepository(_fixture.ContextFactory);
        transactions.Add(customAccountId, categoryId: 3, amountKopecks: 100, date: new DateOnly(2026, 3, 5), comment: null);

        _fixture.ResetToSeed();

        // Сидовые записи не удаляются и не дублируются: те же две записи
        // с теми же идентификаторами, RemoveRange их не трогает.
        IReadOnlyList<Account> seedAccounts = accounts.ListAll();
        Assert.Equal(2, seedAccounts.Count);
        Assert.Equal(1, seedAccounts[0].Id);
        Assert.Equal("Наличные", seedAccounts[0].Name);
        Assert.Equal(2, seedAccounts[1].Id);
        Assert.Equal("Карта", seedAccounts[1].Name);

        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Assert.Equal(0, context.Transactions.Count());
        Assert.Equal(6, context.Categories.Count());
    }

    [Fact]
    public void DateOnly_RoundTripsLeapDayAndDecember31_StoresIsoText()
    {
        var repository = new EfTransactionRepository(_fixture.ContextFactory);
        long leapDay = repository.Add(1, 3, 100, new DateOnly(2028, 2, 29), null);
        long december31 = repository.Add(1, 4, 200, new DateOnly(2025, 12, 31), null);

        Transaction? loadedLeapDay = repository.GetById(leapDay);
        Transaction? loadedDecember31 = repository.GetById(december31);

        Assert.Equal(new DateOnly(2028, 2, 29), loadedLeapDay!.Date);
        Assert.Equal(new DateOnly(2025, 12, 31), loadedDecember31!.Date);

        // В столбце — текст ISO «ГГГГ-ММ-ДД», как в спецификации модели.
        Assert.Equal("2028-02-29", ReadScalar(_fixture.DatabasePath, $"SELECT date FROM transactions WHERE id = {leapDay};"));
        Assert.Equal("2025-12-31", ReadScalar(_fixture.DatabasePath, $"SELECT date FROM transactions WHERE id = {december31};"));

        // Високосный день участвует и в выборке по периоду.
        Assert.Single(repository.ListByPeriod(new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29)));
    }

    [Fact]
    public void CategoryKind_BothKinds_RoundTripThroughValueConverter()
    {
        var repository = new EfCategoryRepository(_fixture.ContextFactory);
        long incomeId = repository.Add("Тестовый доход", CategoryKind.Income, hasLimit: false, sortOrder: 0);
        long expenseId = repository.Add("Тестовый расход", CategoryKind.Expense, hasLimit: true, sortOrder: 0);

        Assert.Equal(CategoryKind.Income, repository.GetById(incomeId)!.Kind);
        Assert.Equal(CategoryKind.Expense, repository.GetById(expenseId)!.Kind);

        // Обе ветки конвертера: в столбце тексты «income»/«expense».
        Assert.Equal("income", ReadScalar(_fixture.DatabasePath, $"SELECT kind FROM categories WHERE id = {incomeId};"));
        Assert.Equal("expense", ReadScalar(_fixture.DatabasePath, $"SELECT kind FROM categories WHERE id = {expenseId};"));
    }

    [Fact]
    public void Navigations_AccountAndCategory_RequireExplicitInclude()
    {
        var repository = new EfTransactionRepository(_fixture.ContextFactory);
        long id = repository.Add(1, 3, 100, new DateOnly(2026, 3, 5), null);

        using (KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            Transaction plain = context.Transactions.AsNoTracking().First(t => t.Id == id);

            // Ленивой загрузки нет: без Include навигации не заполнены.
            Assert.Null(plain.Account);
            Assert.Null(plain.Category);
        }

        using (KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            Transaction loaded = context.Transactions
                .AsNoTracking()
                .Include(t => t.Account)
                .Include(t => t.Category)
                .First(t => t.Id == id);

            Assert.Equal("Наличные", loaded.Account!.Name);
            Assert.Equal("Продукты", loaded.Category!.Name);
            Assert.Equal(CategoryKind.Expense, loaded.Category.Kind);
        }
    }

    private static void ExecuteRaw(string databasePath, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string ReadScalar(string databasePath, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return command.ExecuteScalar()!.ToString()!;
    }
}
