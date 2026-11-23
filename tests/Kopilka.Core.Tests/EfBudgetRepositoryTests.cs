using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Репозиторий бюджетов на реальной SQLite-базе: уникальность пары
/// (категория, месяц), upsert без дублей, ограничения схемы.
/// </summary>
public class EfBudgetRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public EfBudgetRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private EfBudgetRepository Repository => new(_fixture.ContextFactory);

    [Fact]
    public void GetByCategoryAndMonth_WhenMissing_ReturnsNull()
    {
        Assert.Null(Repository.GetByCategoryAndMonth(3, "2026-11"));
    }

    [Fact]
    public void Set_CreatesBudget()
    {
        var repository = Repository;

        repository.Set(3, "2026-11", 15_000_00);

        Budget budget = repository.GetByCategoryAndMonth(3, "2026-11")!;
        Assert.NotNull(budget);
        Assert.Equal(15_000_00, budget.LimitKopecks);
        Assert.Equal("2026-11", budget.Month);
    }

    [Fact]
    public void Set_OnExistingPair_UpdatesLimit_WithoutDuplicate()
    {
        var repository = Repository;
        repository.Set(3, "2026-11", 15_000_00);

        repository.Set(3, "2026-11", 18_000_00);

        Assert.Equal(18_000_00, repository.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);
        Assert.Single(repository.ListByMonth("2026-11"));
    }

    [Fact]
    public void ListByMonth_ReturnsOnlyThatMonth()
    {
        var repository = Repository;
        repository.Set(3, "2026-11", 15_000_00);
        repository.Set(4, "2026-11", 5_000_00);
        repository.Set(3, "2026-12", 16_000_00);

        Assert.Equal(2, repository.ListByMonth("2026-11").Count);
        Assert.Single(repository.ListByMonth("2026-12"));
        Assert.Empty(repository.ListByMonth("2027-01"));
    }

    [Fact]
    public void Remove_Existing_ReturnsTrue_Removes()
    {
        var repository = Repository;
        repository.Set(3, "2026-11", 15_000_00);

        Assert.True(repository.Remove(3, "2026-11"));

        Assert.Null(repository.GetByCategoryAndMonth(3, "2026-11"));
        Assert.False(repository.Remove(3, "2026-11"));
    }

    [Fact]
    public void Set_ZeroOrNegativeLimit_IsRejectedByDatabaseCheck()
    {
        var repository = Repository;

        Assert.Throws<DbUpdateException>(() => repository.Set(3, "2026-11", 0));
        Assert.Throws<DbUpdateException>(() => repository.Set(3, "2026-12", -100));
    }

    [Fact]
    public void Set_UnknownCategory_IsRejectedByForeignKey()
    {
        var repository = Repository;

        Assert.Throws<DbUpdateException>(() => repository.Set(999, "2026-11", 1000));
    }

    [Fact]
    public void SumByCategory_GroupsAmountsWithinPeriod()
    {
        var transactions = new EfTransactionRepository(_fixture.ContextFactory);
        transactions.Add(1, 3, 1_000, new DateOnly(2026, 11, 5), null);
        transactions.Add(2, 3, 2_000, new DateOnly(2026, 11, 20), null);
        transactions.Add(1, 4, 500, new DateOnly(2026, 11, 30), null);
        transactions.Add(1, 3, 9_999, new DateOnly(2026, 12, 1), null); // вне периода

        IReadOnlyDictionary<long, long> sums = transactions.SumByCategory(
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        Assert.Equal(2, sums.Count);
        Assert.Equal(3_000, sums[3]);
        Assert.Equal(500, sums[4]);
    }
}
