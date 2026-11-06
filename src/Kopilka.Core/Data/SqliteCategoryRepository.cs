using Kopilka.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Kopilka.Core.Data;

/// <summary>Справочник категорий поверх SQLite.</summary>
public sealed class SqliteCategoryRepository : ICategoryRepository
{
    private readonly SqliteConnection _connection;

    public SqliteCategoryRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public IReadOnlyList<Category> ListAll()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, kind, has_limit, sort_order
            FROM categories
            ORDER BY kind, sort_order, name;
            """;

        using SqliteDataReader reader = command.ExecuteReader();
        var categories = new List<Category>();
        while (reader.Read())
        {
            categories.Add(ReadCategory(reader));
        }

        return categories;
    }

    public Category? GetById(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, kind, has_limit, sort_order
            FROM categories
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadCategory(reader) : null;
    }

    public long Add(string name, CategoryKind kind, bool hasLimit, int sortOrder)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO categories (name, kind, has_limit, sort_order)
            VALUES (@name, @kind, @has_limit, @sort_order)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@kind", KindToString(kind));
        command.Parameters.AddWithValue("@has_limit", hasLimit ? 1 : 0);
        command.Parameters.AddWithValue("@sort_order", sortOrder);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    public bool Update(Category category)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            UPDATE categories
            SET name = @name, kind = @kind, has_limit = @has_limit, sort_order = @sort_order
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", category.Id);
        command.Parameters.AddWithValue("@name", category.Name);
        command.Parameters.AddWithValue("@kind", KindToString(category.Kind));
        command.Parameters.AddWithValue("@has_limit", category.HasLimit ? 1 : 0);
        command.Parameters.AddWithValue("@sort_order", category.SortOrder);

        return command.ExecuteNonQuery() > 0;
    }

    public DeleteResult Delete(long id)
    {
        if (IsUsed(id))
        {
            return DeleteResult.InUse;
        }

        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM categories WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        return command.ExecuteNonQuery() > 0 ? DeleteResult.Deleted : DeleteResult.NotFound;
    }

    private bool IsUsed(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM transactions WHERE category_id = @id
            );
            """;
        command.Parameters.AddWithValue("@id", id);

        return Convert.ToInt64(command.ExecuteScalar()) == 1;
    }

    private static Category ReadCategory(SqliteDataReader reader) => new(
        Id: reader.GetInt64(0),
        Name: reader.GetString(1),
        Kind: ReadKind(reader.GetString(2)),
        HasLimit: reader.GetInt64(3) == 1,
        SortOrder: reader.GetInt32(4));

    private static string KindToString(CategoryKind kind) => kind switch
    {
        CategoryKind.Income => "income",
        CategoryKind.Expense => "expense",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный тип категории"),
    };

    private static CategoryKind ReadKind(string raw) => raw == "income"
        ? CategoryKind.Income
        : CategoryKind.Expense;
}
