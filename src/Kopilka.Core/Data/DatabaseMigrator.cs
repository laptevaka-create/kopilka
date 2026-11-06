using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Kopilka.Core.Data;

/// <summary>
/// Применяет SQL-миграции к базе. Версия схемы хранится в самом файле
/// базы стандартным механизмом SQLite — PRAGMA user_version.
/// Миграции — SQL-файлы V###__имя.sql, встроенные в сборку; применяются
/// по порядку номеров, каждая в отдельной транзакции: сбой откатывает
/// только свою миграцию, а не весь файл базы.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly SqliteConnection _connection;

    public DatabaseMigrator(SqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>Текущая версия схемы базы (PRAGMA user_version).</summary>
    public int CurrentVersion
    {
        get
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Применяет все миграции с номером больше текущей версии.
    /// Возвращает версию после применения.
    /// </summary>
    public int Migrate()
    {
        int current = CurrentVersion;
        foreach ((int version, string script) in LoadMigrations().Where(m => m.Version > current))
        {
            Apply(version, script);
        }

        return CurrentVersion;
    }

    private void Apply(int version, string script)
    {
        using SqliteTransaction transaction = _connection.BeginTransaction();
        try
        {
            foreach (string statement in SplitScript(script))
            {
                using var command = _connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }

            using var setVersion = _connection.CreateCommand();
            setVersion.Transaction = transaction;
            setVersion.CommandText = $"PRAGMA user_version = {version.ToString(CultureInfo.InvariantCulture)};";
            setVersion.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Читает миграции из встроенных ресурсов сборки:
    /// имя вида Kopilka.Core.Data.Migrations.V001__init.sql.
    /// </summary>
    private static IEnumerable<(int Version, string Script)> LoadMigrations()
    {
        Assembly assembly = typeof(DatabaseMigrator).Assembly;
        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            Match match = Regex.Match(resourceName, @"V(\d+)__.*\.sql$");
            if (!match.Success)
            {
                continue;
            }

            int version = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Ресурс миграции не читается: {resourceName}");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            yield return (version, reader.ReadToEnd());
        }
    }

    /// <summary>
    /// Разбивает скрипт на предложения по «;». В наших миграциях нет строк
    /// с точкой с запятой внутри, поэтому простое разделение безопасно
    /// и предсказуемо.
    /// </summary>
    private static IEnumerable<string> SplitScript(string script) =>
        script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(statement => statement.Length > 0);
}
