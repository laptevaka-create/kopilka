using Kopilka.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Kopilka.Core.Data;

/// <summary>Справочник счетов поверх SQLite.</summary>
public sealed class SqliteAccountRepository : IAccountRepository
{
    private readonly SqliteConnection _connection;

    public SqliteAccountRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public IReadOnlyList<Account> ListAll()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, sort_order
            FROM accounts
            ORDER BY sort_order, name;
            """;

        using SqliteDataReader reader = command.ExecuteReader();
        var accounts = new List<Account>();
        while (reader.Read())
        {
            accounts.Add(ReadAccount(reader));
        }

        return accounts;
    }

    public Account? GetById(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, sort_order
            FROM accounts
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadAccount(reader) : null;
    }

    public long Add(string name, int sortOrder)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounts (name, sort_order)
            VALUES (@name, @sort_order)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@sort_order", sortOrder);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    public bool Update(Account account)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            UPDATE accounts
            SET name = @name, sort_order = @sort_order
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", account.Id);
        command.Parameters.AddWithValue("@name", account.Name);
        command.Parameters.AddWithValue("@sort_order", account.SortOrder);

        return command.ExecuteNonQuery() > 0;
    }

    public DeleteResult Delete(long id)
    {
        if (IsUsed(id))
        {
            return DeleteResult.InUse;
        }

        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM accounts WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        return command.ExecuteNonQuery() > 0 ? DeleteResult.Deleted : DeleteResult.NotFound;
    }

    private bool IsUsed(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM transactions WHERE account_id = @id
            );
            """;
        command.Parameters.AddWithValue("@id", id);

        return Convert.ToInt64(command.ExecuteScalar()) == 1;
    }

    private static Account ReadAccount(SqliteDataReader reader) => new(
        Id: reader.GetInt64(0),
        Name: reader.GetString(1),
        SortOrder: reader.GetInt32(2));
}
