using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Kopilka.Core.Tests;

public class CategoryRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public CategoryRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private SqliteCategoryRepository Repository => new(_fixture.Database.Connection);

    [Fact]
    public void ListAll_ReturnsPresetCategories()
    {
        IReadOnlyList<Category> categories = Repository.ListAll();

        Assert.Equal(6, categories.Count);
    }

    [Fact]
    public void Add_CreatesCategory_WithGivenKind()
    {
        long id = Repository.Add("Кофейня", CategoryKind.Expense, hasLimit: true, sortOrder: 10);

        Category? loaded = Repository.GetById(id);

        Assert.NotNull(loaded);
        Assert.Equal(CategoryKind.Expense, loaded!.Kind);
        Assert.True(loaded.HasLimit);
    }

    [Fact]
    public void Add_DuplicateNameWithinSameKind_IsRejected()
    {
        Repository.Add("Уникальная категория", CategoryKind.Expense, hasLimit: false, sortOrder: 0);

        Assert.Throws<SqliteException>(() =>
            Repository.Add("Уникальная категория", CategoryKind.Expense, hasLimit: false, sortOrder: 1));
    }

    [Fact]
    public void Add_SameNameInDifferentKinds_IsAllowed()
    {
        // Имя уникально внутри типа, а не глобально: «Прочие» может быть и доходом, и расходом.
        long incomeId = Repository.Add("Общая", CategoryKind.Income, hasLimit: false, sortOrder: 0);
        long expenseId = Repository.Add("Общая", CategoryKind.Expense, hasLimit: false, sortOrder: 0);

        Assert.NotEqual(incomeId, expenseId);
    }

    [Fact]
    public void Update_ChangesNameAndLimitFlag()
    {
        var repository = Repository;
        long id = repository.Add("Было", CategoryKind.Expense, hasLimit: false, sortOrder: 0);

        bool updated = repository.Update(new Category(id, "Стало", CategoryKind.Expense, HasLimit: true, SortOrder: 0));

        Assert.True(updated);
        Category? loaded = repository.GetById(id);
        Assert.Equal("Стало", loaded!.Name);
        Assert.True(loaded.HasLimit);
    }

    [Fact]
    public void Delete_UnusedCategory_ReturnsDeleted()
    {
        var repository = Repository;
        long id = repository.Add("Временная", CategoryKind.Income, hasLimit: false, sortOrder: 0);

        DeleteResult result = repository.Delete(id);

        Assert.Equal(DeleteResult.Deleted, result);
    }

    [Fact]
    public void Delete_CategoryWithTransactions_ReturnsInUse()
    {
        var repository = Repository;
        long id = repository.Add("Используемая", CategoryKind.Expense, hasLimit: false, sortOrder: 0);
        var transactions = new SqliteTransactionRepository(_fixture.Database.Connection);
        transactions.Add(accountId: 1, categoryId: id, amountKopecks: 500, date: new DateOnly(2026, 1, 1), comment: null);

        DeleteResult result = repository.Delete(id);

        Assert.Equal(DeleteResult.InUse, result);
    }
}
