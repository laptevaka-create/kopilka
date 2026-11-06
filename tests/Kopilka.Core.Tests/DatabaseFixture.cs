using Kopilka.Core.Data;
using Microsoft.Data.Sqlite;

namespace Kopilka.Core.Tests;

/// <summary>
/// Своя временная база для каждого тестового класса: миграция применяется
/// один раз, тесты класса работают с изолированной копией.
/// </summary>
public sealed class DatabaseFixture : IDisposable
{
    public SqliteDatabase Database { get; }

    public string DatabasePath { get; }

    public string DataDirectory { get; }

    public DatabaseFixture()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "kopilka-tests", Guid.NewGuid().ToString("N"));
        DatabasePath = Path.Combine(DataDirectory, "test.db");
        Directory.CreateDirectory(DataDirectory);

        Database = new SqliteDatabase(DatabasePath);
        new DatabaseMigrator(Database.Connection).Migrate();
    }

    public void Dispose()
    {
        Database.Dispose();

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

    /// <summary>
    /// Возвращает базу к состоянию сразу после миграции: транзакции
    /// удалены, справочники — только предустановленные записи.
    /// Вызывается перед каждым тестом: тесты не зависят от порядка
    /// выполнения и от данных друг друга.
    /// </summary>
    public void ResetToSeed()
    {
        Execute("DELETE FROM transactions;");
        Execute("DELETE FROM accounts WHERE id > 2;");
        Execute("DELETE FROM categories WHERE id > 6;");
    }

    private void Execute(string sql)
    {
        using var command = Database.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
