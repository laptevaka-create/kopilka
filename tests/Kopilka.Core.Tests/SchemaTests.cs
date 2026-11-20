using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Проверки схемы, созданной миграциями EF Core: таблицы, сид,
/// идемпотентность применения, журнал миграций и целостность.
/// </summary>
public class SchemaTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public SchemaTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Migrate_AppliesInitialCreateMigration()
    {
        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();

        string[] applied = context.Database.GetAppliedMigrations().ToArray();

        // Имя миграции с префиксом времени: «20261114…_InitialCreate».
        Assert.Contains(applied, m => m.EndsWith("InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public void Migrate_SecondRun_IsIdempotent()
    {
        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();

        // Повторное применение миграций: применённых нет, ничего не меняется.
        context.Database.Migrate();

        Assert.Single(context.Database.GetAppliedMigrations());
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
        var repository = new EfAccountRepository(_fixture.ContextFactory);

        IReadOnlyList<Account> accounts = repository.ListAll();

        Assert.Contains(accounts, a => a.Name == "Наличные");
        Assert.Contains(accounts, a => a.Name == "Карта");
    }

    [Fact]
    public void Migrate_SeedsPresetCategories()
    {
        var repository = new EfCategoryRepository(_fixture.ContextFactory);

        IReadOnlyList<Category> categories = repository.ListAll();

        Assert.Contains(categories, c => c.Name == "Зарплата" && c.Kind == CategoryKind.Income);
        Assert.Contains(categories, c => c.Name == "Продукты" && c.Kind == CategoryKind.Expense);
    }

    [Fact]
    public void MigrationsHistoryTable_ExistsInDatabase()
    {
        string[] tables = ReadTableNames();

        // Журнал миграций EF Core хранится в самой базе.
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    private string[] ReadTableNames()
    {
        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
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
