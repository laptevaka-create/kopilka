using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контракт EF-репозитория шаблонов: round-trip полей, сортировка списка,
/// ответы на отсутствующий id и сохранение LastApplied при редактировании.
/// Все идентификаторы получены из Add — sqlite_sequence между тестами
/// специально не сбрасывается.
/// </summary>
public class EfRecurringPaymentRepositoryContractTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public EfRecurringPaymentRepositoryContractTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private EfRecurringPaymentRepository Repository => new(_fixture.ContextFactory);

    [Fact]
    public void Add_ValidTemplate_RoundTripsAllFields()
    {
        long id = Repository.Add(3, 2, 25_000_00, 31, "аренда", isActive: false);

        Assert.True(id > 0);
        RecurringPayment loaded = Repository.GetById(id)!;
        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.CategoryId);
        Assert.Equal(2, loaded.AccountId);
        Assert.Equal(25_000_00, loaded.AmountKopecks);
        Assert.Equal(31, loaded.DayOfMonth);
        Assert.Equal("аренда", loaded.Comment);
        Assert.False(loaded.IsActive);
        Assert.Null(loaded.LastApplied);
    }

    [Fact]
    public void Add_TwoTemplates_ReturnsDistinctIncreasingIds()
    {
        long firstId = Repository.Add(3, 1, 100_00, 5, null, isActive: true);
        long secondId = Repository.Add(4, 2, 200_00, 10, null, isActive: true);

        Assert.NotEqual(firstId, secondId);
        Assert.True(secondId > firstId);
    }

    [Fact]
    public void ListAll_SeveralTemplates_OrdersByDayOfMonthThenId()
    {
        long lateId = Repository.Add(3, 1, 100_00, 20, null, isActive: true);
        long firstEarlyId = Repository.Add(3, 1, 100_00, 5, null, isActive: true);
        long secondEarlyId = Repository.Add(4, 2, 200_00, 5, null, isActive: true);

        RecurringPayment[] templates = Repository.ListAll().ToArray();

        Assert.Equal(new[] { 5, 5, 20 }, templates.Select(t => t.DayOfMonth).ToArray());
        Assert.Equal(new[] { firstEarlyId, secondEarlyId, lateId }, templates.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        Assert.Null(Repository.GetById(long.MaxValue));
    }

    [Fact]
    public void Update_ExistingTemplate_ReplacesEditableFieldsButPreservesLastApplied()
    {
        long id = Repository.Add(3, 2, 500_00, 5, "старый", isActive: true);
        var lastApplied = new DateOnly(2026, 11, 5);
        using (KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            RecurringPayment stored = context.RecurringPayments.Single(r => r.Id == id);
            stored.LastApplied = lastApplied;
            context.SaveChanges();
        }

        // Модель окна создаёт объект без LastApplied. Update обязан не
        // стирать отметку: иначе следующее начисление потеряет историю.
        var replacement = new RecurringPayment
        {
            Id = id,
            CategoryId = 4,
            AccountId = 1,
            AmountKopecks = 900_00,
            DayOfMonth = 10,
            Comment = null,
            IsActive = false,
            LastApplied = null,
        };
        Assert.True(Repository.Update(replacement));

        RecurringPayment loaded = Repository.GetById(id)!;
        Assert.Equal(4, loaded.CategoryId);
        Assert.Equal(1, loaded.AccountId);
        Assert.Equal(900_00, loaded.AmountKopecks);
        Assert.Equal(10, loaded.DayOfMonth);
        Assert.Null(loaded.Comment);
        Assert.False(loaded.IsActive);
        Assert.Equal(lastApplied, loaded.LastApplied);
    }

    [Fact]
    public void Update_UnknownTemplate_ReturnsFalse()
    {
        var replacement = new RecurringPayment
        {
            Id = long.MaxValue,
            CategoryId = 3,
            AccountId = 1,
            AmountKopecks = 100_00,
            DayOfMonth = 5,
            IsActive = true,
        };

        Assert.False(Repository.Update(replacement));
    }

    [Fact]
    public void Delete_ExistingThenMissing_ReturnsTrueThenFalse()
    {
        long id = Repository.Add(3, 1, 100_00, 5, null, isActive: true);

        Assert.True(Repository.Delete(id));
        Assert.Null(Repository.GetById(id));
        Assert.False(Repository.Delete(id));
        Assert.False(Repository.Delete(long.MaxValue));
    }
}
