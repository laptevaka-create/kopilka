using Kopilka.Core.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Kopilka.Core.Tests;

public class DatabaseMigratorTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public DatabaseMigratorTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Migrate_SetsSchemaVersionToOne()
    {
        var migrator = new DatabaseMigrator(_fixture.Database.Connection);

        Assert.Equal(1, migrator.CurrentVersion);
    }

    [Fact]
    public void Migrate_SecondRun_IsIdempotent()
    {
        var migrator = new DatabaseMigrator(_fixture.Database.Connection);

        int versionAfterSecondRun = migrator.Migrate();

        Assert.Equal(1, versionAfterSecondRun);
    }

    [Fact]
    public void Migrate_CreatesAllTables()
    {
        string[] tables = ReadTableNames();

        foreach (string expected in new[] { "accounts", "categories", "transactions", "budgets", "recurring", "settings" })
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public void Migrate_SeedsPresetAccounts()
    {
        var repository = new SqliteAccountRepository(_fixture.Database.Connection);

        IReadOnlyList<Kopilka.Core.Domain.Account> accounts = repository.ListAll();

        Assert.Contains(accounts, a => a.Name == "Наличные");
        Assert.Contains(accounts, a => a.Name == "Карта");
    }

    [Fact]
    public void Migrate_SeedsPresetCategories()
    {
        var repository = new SqliteCategoryRepository(_fixture.Database.Connection);

        IReadOnlyList<Kopilka.Core.Domain.Category> categories = repository.ListAll();

        Assert.Contains(categories, c => c.Name == "Зарплата" && c.Kind == Kopilka.Core.Domain.CategoryKind.Income);
        Assert.Contains(categories, c => c.Name == "Продукты" && c.Kind == Kopilka.Core.Domain.CategoryKind.Expense);
    }

    [Fact]
    public void Database_UsesWalJournalMode()
    {
        using var command = _fixture.Database.Connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";

        string mode = command.ExecuteScalar()!.ToString()!;

        Assert.Equal("wal", mode, ignoreCase: true);
    }

    [Fact]
    public void Database_EnforcesForeignKeys()
    {
        // Транзакция на несуществующий счёт должна быть отвергнута.
        using var command = _fixture.Database.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO transactions (account_id, category_id, amount_kopecks, date, comment)
            VALUES (999, 1, 100, '2026-01-01', NULL);
            """;

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    private string[] ReadTableNames()
    {
        using var command = _fixture.Database.Connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";

        using SqliteDataReader reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names.ToArray();
    }
}
