using Kopilka.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Kopilka.Core.Tests;

/// <summary>
/// Своя временная база для каждого тестового класса: миграции EF Core
/// применяются один раз, тесты класса работают с изолированной копией.
/// </summary>
public sealed class DatabaseFixture : IDisposable
{
    public IDbContextFactory<KopilkaDbContext> ContextFactory { get; }

    public string DatabasePath { get; }

    public string DataDirectory { get; }

    public DatabaseFixture()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "kopilka-tests", Guid.NewGuid().ToString("N"));
        DatabasePath = Path.Combine(DataDirectory, "test.db");
        Directory.CreateDirectory(DataDirectory);

        ContextFactory = new SimpleDbContextFactory(DatabasePath);
        using KopilkaDbContext context = ContextFactory.CreateDbContext();
        context.Database.Migrate();
    }

    /// <summary>
    /// Возвращает базу к состоянию сразу после миграции: транзакции
    /// удалены, справочники — только предустановленные записи.
    /// Вызывается перед каждым тестом: тесты не зависят от порядка
    /// выполнения и от данных друг друга.
    /// </summary>
    public void ResetToSeed()
    {
        using KopilkaDbContext context = ContextFactory.CreateDbContext();
        context.Transactions.RemoveRange(context.Transactions);
        context.Accounts.RemoveRange(context.Accounts.Where(a => a.Id > 2));
        context.Categories.RemoveRange(context.Categories.Where(c => c.Id > 6));
        context.SaveChanges();
    }

    public void Dispose()
    {
        // WAL создаёт рядом -wal и -shm файлы; чистим весь каталог.
        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Файлы могут ещё удерживаться драйвером — временную папку
            // подчистит система; тесту это не мешает.
        }
    }

    /// <summary>Простая фабрика контекста для тестов: путь к базе фиксирован.</summary>
    private sealed class SimpleDbContextFactory(string databasePath) : IDbContextFactory<KopilkaDbContext>
    {
        public KopilkaDbContext CreateDbContext()
        {
            DbContextOptions<KopilkaDbContext> options = new DbContextOptionsBuilder<KopilkaDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            return new KopilkaDbContext(options);
        }
    }
}
