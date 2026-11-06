using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контрактные тесты справочника категорий: несуществующие
/// идентификаторы (GetById/Update/Delete), сохранение has_limit = 1,
/// дубликаты имён против предустановленных категорий, смена типа
/// категории через Update и фактический порядок ListAll.
/// </summary>
public class CategoryRepositoryContractTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public CategoryRepositoryContractTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private SqliteCategoryRepository Repository => new(_fixture.Database.Connection);

    // Сид V001: 1 «Зарплата», 2 «Прочие доходы» (доходы);
    // 3 «Продукты», 4 «Транспорт», 5 «Развлечения», 6 «Прочие расходы» (расходы).

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        Category? loaded = Repository.GetById(9999);

        Assert.Null(loaded);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        bool updated = Repository.Update(
            new Category(9999, "Несуществующая", CategoryKind.Expense, HasLimit: false, SortOrder: 0));

        Assert.False(updated);
    }

    [Fact]
    public void Delete_UnknownId_ReturnsNotFound()
    {
        DeleteResult result = Repository.Delete(9999);

        Assert.Equal(DeleteResult.NotFound, result);
    }

    [Fact]
    public void Add_HasLimitTrue_PersistsFlagAsOne()
    {
        var repository = Repository;
        long id = repository.Add("Кафе и рестораны", CategoryKind.Expense, hasLimit: true, sortOrder: 0);

        Category? loaded = repository.GetById(id);

        Assert.NotNull(loaded);
        Assert.True(loaded!.HasLimit);
        Assert.Equal(1, ReadRawHasLimit(id));
    }

    [Fact]
    public void Add_DuplicateOfPresetNameWithinSameKind_IsRejected()
    {
        Assert.Throws<SqliteException>(() =>
            Repository.Add("Продукты", CategoryKind.Expense, hasLimit: false, sortOrder: 0));
        Assert.Throws<SqliteException>(() =>
            Repository.Add("Зарплата", CategoryKind.Income, hasLimit: false, sortOrder: 0));
    }

    [Fact]
    public void Add_PresetNameInDifferentKind_IsAllowed()
    {
        // Имя уникально внутри типа, а не глобально: «Зарплата»-расход
        // не конфликтует с «Зарплата»-доходом из сида.
        long id = Repository.Add("Зарплата", CategoryKind.Expense, hasLimit: false, sortOrder: 0);

        Category? loaded = Repository.GetById(id);
        Assert.NotNull(loaded);
        Assert.Equal("Зарплата", loaded!.Name);
        Assert.Equal(CategoryKind.Expense, loaded.Kind);
    }

    [Fact]
    public void Update_ChangesKind_ExpenseToIncome()
    {
        var repository = Repository;
        long id = repository.Add("Подписки", CategoryKind.Expense, hasLimit: false, sortOrder: 0);

        bool updated = repository.Update(
            new Category(id, "Подписки", CategoryKind.Income, HasLimit: false, SortOrder: 0));

        Assert.True(updated);
        Assert.Equal(CategoryKind.Income, repository.GetById(id)!.Kind);
    }

    [Fact]
    public void ListAll_OrdersByKindThenSortOrderThenName()
    {
        // Фактический порядок выборки: расходы раньше доходов
        // ('expense' < 'income' в BINARY-коллации), внутри типа — по
        // sort_order. Предустановленные sort_order: расходы 0..3, доходы 0..1.
        string[] names = Repository.ListAll().Select(c => c.Name).ToArray();

        Assert.Equal(
            new[] { "Продукты", "Транспорт", "Развлечения", "Прочие расходы", "Зарплата", "Прочие доходы" },
            names);
    }

    private long ReadRawHasLimit(long id)
    {
        using var command = _fixture.Database.Connection.CreateCommand();
        command.CommandText = "SELECT has_limit FROM categories WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        return Convert.ToInt64(command.ExecuteScalar());
    }
}
