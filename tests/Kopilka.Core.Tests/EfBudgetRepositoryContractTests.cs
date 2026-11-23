using Kopilka.Core.Data;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контрактные тесты хранилища бюджетов на реальной SQLite-базе:
/// изоляция категорий и месяцев, границы периода в SumByCategory,
/// группировка категорий одного дня, 29 февраля високосного года.
/// </summary>
public class EfBudgetRepositoryContractTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public EfBudgetRepositoryContractTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private EfBudgetRepository Repository => new(_fixture.ContextFactory);

    private EfTransactionRepository Transactions => new(_fixture.ContextFactory);

    // Сид миграции: категории 3 «Продукты», 4 «Транспорт», 5 «Развлечения».

    [Fact]
    public void Set_DifferentCategoriesInSameMonth_AreKeptSeparately()
    {
        var repository = Repository;
        repository.Set(3, "2026-11", 15_000_00);
        repository.Set(5, "2026-11", 5_000_00);

        // Обновление бюджета одной категории не трогает другую.
        repository.Set(5, "2026-11", 7_000_00);

        Assert.Equal(15_000_00, repository.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);
        Assert.Equal(7_000_00, repository.GetByCategoryAndMonth(5, "2026-11")!.LimitKopecks);
        Assert.Equal(2, repository.ListByMonth("2026-11").Count);
    }

    [Fact]
    public void Remove_BudgetOfOneMonth_KeepsBudgetsOfOtherMonths()
    {
        var repository = Repository;
        repository.Set(3, "2026-11", 15_000_00);
        repository.Set(3, "2026-12", 16_000_00);
        repository.Set(3, "2027-01", 17_000_00);

        Assert.True(repository.Remove(3, "2026-12"));

        Assert.Null(repository.GetByCategoryAndMonth(3, "2026-12"));
        Assert.Equal(15_000_00, repository.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);
        Assert.Equal(17_000_00, repository.GetByCategoryAndMonth(3, "2027-01")!.LimitKopecks);
        Assert.Empty(repository.ListByMonth("2026-12"));
    }

    [Fact]
    public void SumByCategory_PeriodWithoutTransactions_ReturnsEmptyDictionary()
    {
        Assert.Empty(Transactions.SumByCategory(
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
    }

    [Fact]
    public void SumByCategory_PeriodBounds_AreInclusive()
    {
        var transactions = Transactions;
        transactions.Add(1, 3, 100, new DateOnly(2026, 11, 1), null);   // первый день
        transactions.Add(1, 3, 200, new DateOnly(2026, 11, 15), null);  // середина
        transactions.Add(1, 3, 300, new DateOnly(2026, 11, 30), null);  // последний день
        transactions.Add(1, 3, 999, new DateOnly(2026, 10, 31), null);  // до периода
        transactions.Add(1, 3, 999, new DateOnly(2026, 12, 1), null);   // после периода

        IReadOnlyDictionary<long, long> sums = transactions.SumByCategory(
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        KeyValuePair<long, long> sum = Assert.Single(sums);
        Assert.Equal(3, sum.Key);
        Assert.Equal(600, sum.Value);
    }

    [Fact]
    public void SumByCategory_TwoCategoriesSameDay_GroupedSeparately()
    {
        var transactions = Transactions;
        transactions.Add(1, 3, 1_000, new DateOnly(2026, 11, 10), null);
        transactions.Add(2, 4, 2_500, new DateOnly(2026, 11, 10), null); // тот же день
        transactions.Add(1, 4, 500, new DateOnly(2026, 11, 10), null);   // и тот же счёт

        IReadOnlyDictionary<long, long> sums = transactions.SumByCategory(
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        Assert.Equal(2, sums.Count);
        Assert.Equal(1_000, sums[3]);
        Assert.Equal(3_000, sums[4]);
    }

    [Fact]
    public void SumByCategory_LeapFebruary29_CountedAndServesAsPeriodBound()
    {
        var transactions = Transactions;
        transactions.Add(1, 3, 100, new DateOnly(2028, 2, 28), null);
        transactions.Add(1, 3, 200, new DateOnly(2028, 2, 29), null); // високосный день
        transactions.Add(1, 3, 400, new DateOnly(2028, 3, 1), null);

        // Весь високосный февраль: 28-е и 29-е входят, 1 марта — нет.
        IReadOnlyDictionary<long, long> february = transactions.SumByCategory(
            new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29));
        Assert.Equal(300, february[3]);

        // 29 февраля работает и как обе границы периода.
        IReadOnlyDictionary<long, long> leapDayOnly = transactions.SumByCategory(
            new DateOnly(2028, 2, 29), new DateOnly(2028, 2, 29));
        Assert.Equal(200, leapDayOnly[3]);
    }
}
