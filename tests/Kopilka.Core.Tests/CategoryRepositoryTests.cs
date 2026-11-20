using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;
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

    private EfCategoryRepository Repository => new(_fixture.ContextFactory);

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

        Assert.Throws<DbUpdateException>(() =>
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

        bool updated = repository.Update(
            new Category { Id = id, Name = "Стало", Kind = CategoryKind.Expense, HasLimit = true, SortOrder = 0 });

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
        var transactions = new EfTransactionRepository(_fixture.ContextFactory);
        transactions.Add(accountId: 1, categoryId: id, amountKopecks: 500, date: new DateOnly(2026, 1, 1), comment: null);

        DeleteResult result = repository.Delete(id);

        Assert.Equal(DeleteResult.InUse, result);
    }

    [Fact]
    public void Delete_CategoryWithBudget_ReturnsInUse()
    {
        // Бюджеты появятся в интерфейсе на шаге 4, но ссылка уже есть:
        // категория с бюджетом удаляться не должна — отказ по причине,
        // а не необработанное исключение от внешнего ключа.
        var repository = Repository;
        var budget = new Budget { CategoryId = 3, Month = "2026-03", LimitKopecks = 10_000 };
        using (Kopilka.Core.Data.KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            context.Budgets.Add(budget);
            context.SaveChanges();
        }

        try
        {
            DeleteResult result = repository.Delete(3);

            Assert.Equal(DeleteResult.InUse, result);
            Assert.NotNull(repository.GetById(3));
        }
        finally
        {
            using Kopilka.Core.Data.KopilkaDbContext cleanup = _fixture.ContextFactory.CreateDbContext();
            Kopilka.Core.Domain.Budget? created = cleanup.Budgets.Find(budget.Id);
            if (created is not null)
            {
                cleanup.Budgets.Remove(created);
                cleanup.SaveChanges();
            }
        }
    }

    [Fact]
    public void Delete_CategoryWithRecurringTemplate_ReturnsInUse()
    {
        // Шаблоны регулярных платежей — шаг 6, ссылка уже существует.
        var repository = Repository;
        var template = new RecurringPayment
        {
            CategoryId = 3,
            AccountId = 1,
            AmountKopecks = 500,
            DayOfMonth = 5,
            Comment = null,
            IsActive = true,
        };
        using (Kopilka.Core.Data.KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            context.RecurringPayments.Add(template);
            context.SaveChanges();
        }

        try
        {
            DeleteResult result = repository.Delete(3);

            Assert.Equal(DeleteResult.InUse, result);
        }
        finally
        {
            using Kopilka.Core.Data.KopilkaDbContext cleanup = _fixture.ContextFactory.CreateDbContext();
            RecurringPayment? created = cleanup.RecurringPayments.Find(template.Id);
            if (created is not null)
            {
                cleanup.RecurringPayments.Remove(created);
                cleanup.SaveChanges();
            }
        }
    }
}
