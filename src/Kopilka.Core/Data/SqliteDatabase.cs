using Microsoft.Data.Sqlite;

namespace Kopilka.Core.Data;

/// <summary>
/// Открывает базу SQLite и держит единственное соединение на всё
/// время работы приложения. Каталог данных должен существовать
/// до открытия (это делает AppPaths.EnsureDataDirectory).
/// </summary>
public sealed class SqliteDatabase : IDisposable
{
    /// <summary>Открытое соединение с базой.</summary>
    public SqliteConnection Connection { get; }

    /// <summary>
    /// Открывает базу в файле databasePath; файл создаётся, если его нет.
    /// </summary>
    public SqliteDatabase(string databasePath)
    {
        Connection = new SqliteConnection($"Data Source={databasePath}");
        Connection.Open();

        // WAL: целостность базы при внезапном выключении питания.
        Execute("PRAGMA journal_mode = WAL;");

        // Внешние ключи включаются на каждое соединение — без этого
        // SQLite по умолчанию не проверяет ссылки.
        Execute("PRAGMA foreign_keys = ON;");
    }

    private void Execute(string pragma)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = pragma;
        command.ExecuteNonQuery();
    }

    public void Dispose() => Connection.Dispose();
}
