using System.Globalization;
using Kopilka.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Kopilka.Core.Data;

/// <summary>Журнал транзакций поверх SQLite.</summary>
public sealed class SqliteTransactionRepository : ITransactionRepository
{
    private readonly SqliteConnection _connection;

    public SqliteTransactionRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public long Add(long accountId, long categoryId, long amountKopecks, DateOnly date, string? comment)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO transactions (account_id, category_id, amount_kopecks, date, comment)
            VALUES (@account_id, @category_id, @amount, @date, @comment)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("@account_id", accountId);
        command.Parameters.AddWithValue("@category_id", categoryId);
        command.Parameters.AddWithValue("@amount", amountKopecks);
        command.Parameters.AddWithValue("@date", DateToString(date));
        AddCommentParameter(command, comment);

        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public Transaction? GetById(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = SelectById();
        command.Parameters.AddWithValue("@id", id);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadTransaction(reader) : null;
    }

    public IReadOnlyList<Transaction> ListByPeriod(DateOnly from, DateOnly to, long? categoryId = null)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT id, account_id, category_id, amount_kopecks, date, comment
            FROM transactions
            WHERE date >= @from AND date <= @to
            """;

        if (categoryId is not null)
        {
            command.CommandText += " AND category_id = @category_id";
            command.Parameters.AddWithValue("@category_id", categoryId.Value);
        }

        command.CommandText += " ORDER BY date, id;";

        command.Parameters.AddWithValue("@from", DateToString(from));
        command.Parameters.AddWithValue("@to", DateToString(to));

        using SqliteDataReader reader = command.ExecuteReader();
        var transactions = new List<Transaction>();
        while (reader.Read())
        {
            transactions.Add(ReadTransaction(reader));
        }

        return transactions;
    }

    public bool Update(Transaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            UPDATE transactions
            SET account_id = @account_id,
                category_id = @category_id,
                amount_kopecks = @amount,
                date = @date,
                comment = @comment
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", transaction.Id);
        command.Parameters.AddWithValue("@account_id", transaction.AccountId);
        command.Parameters.AddWithValue("@category_id", transaction.CategoryId);
        command.Parameters.AddWithValue("@amount", transaction.AmountKopecks);
        command.Parameters.AddWithValue("@date", DateToString(transaction.Date));
        AddCommentParameter(command, transaction.Comment);

        return command.ExecuteNonQuery() > 0;
    }

    public bool Delete(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM transactions WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        return command.ExecuteNonQuery() > 0;
    }

    private static string SelectById() => """
        SELECT id, account_id, category_id, amount_kopecks, date, comment
        FROM transactions
        WHERE id = @id;
        """;

    private static Transaction ReadTransaction(SqliteDataReader reader) => new(
        Id: reader.GetInt64(0),
        AccountId: reader.GetInt64(1),
        CategoryId: reader.GetInt64(2),
        AmountKopecks: reader.GetInt64(3),
        Date: DateOnly.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
        Comment: reader.IsDBNull(5) ? null : reader.GetString(5));

    /// <summary>Дата хранится строкой «ГГГГ-ММ-ДД»: сортируется как текст, читается глазами.</summary>
    private static string DateToString(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void AddCommentParameter(SqliteCommand command, string? comment)
    {
        command.Parameters.Add(new SqliteParameter("@comment", comment is null ? DBNull.Value : comment));
    }
}
