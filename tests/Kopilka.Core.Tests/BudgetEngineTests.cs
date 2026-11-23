using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Движок статусов бюджета: порог 80 % включительно, «превышен»
/// от лимита, «доступно в день» с текущим днём в остатке, защита
/// от некорректных входов.
/// </summary>
public class BudgetEngineTests
{
    [Fact]
    public void ZeroSpent_IsOk_WithFullLimitPerDay()
    {
        BudgetSnapshot snapshot = BudgetEngine.Compute(
            limitKopecks: 30_000_00, spentKopecks: 0, currentDay: 1, daysInMonth: 30);

        Assert.Equal(BudgetStatus.Ok, snapshot.Status);
        Assert.Equal(30_000_00, snapshot.RemainingKopecks);
        Assert.Equal(1_000_00, snapshot.AvailablePerDayKopecks);
    }

    [Fact]
    public void JustBelowThreshold_IsOk()
    {
        // 79,99 % от 10 000,00 ₽ — ещё «в норме».
        BudgetSnapshot snapshot = BudgetEngine.Compute(
            limitKopecks: 10_000_00, spentKopecks: 7_999_99, currentDay: 10, daysInMonth: 30);

        Assert.Equal(BudgetStatus.Ok, snapshot.Status);
    }

    [Fact]
    public void ExactlyEightyPercent_IsAttention_ThresholdIsInclusive()
    {
        // Ровно 80 % — уже «внимание»: порог включительный.
        BudgetSnapshot snapshot = BudgetEngine.Compute(
            limitKopecks: 10_000_00, spentKopecks: 8_000_00, currentDay: 10, daysInMonth: 30);

        Assert.Equal(BudgetStatus.Attention, snapshot.Status);
    }

    [Fact]
    public void JustBelowLimit_IsStillAttention()
    {
        BudgetSnapshot snapshot = BudgetEngine.Compute(
            limitKopecks: 10_000_00, spentKopecks: 9_999_99, currentDay: 10, daysInMonth: 30);

        Assert.Equal(BudgetStatus.Attention, snapshot.Status);
        Assert.Equal(1, snapshot.RemainingKopecks);
    }

    [Fact]
    public void ExactlyHundredPercent_IsExceeded()
    {
        BudgetSnapshot snapshot = BudgetEngine.Compute(
            limitKopecks: 10_000_00, spentKopecks: 10_000_00, currentDay: 10, daysInMonth: 30);

        Assert.Equal(BudgetStatus.Exceeded, snapshot.Status);
        Assert.Equal(0, snapshot.RemainingKopecks);
        Assert.Null(snapshot.AvailablePerDayKopecks);
    }

    [Fact]
    public void OverLimit_IsExceeded_WithNegativeRemainder()
    {
        BudgetSnapshot snapshot = BudgetEngine.Compute(
            limitKopecks: 10_000_00, spentKopecks: 12_500_00, currentDay: 10, daysInMonth: 30);

        Assert.Equal(BudgetStatus.Exceeded, snapshot.Status);
        Assert.Equal(-2_500_00, snapshot.RemainingKopecks);
        Assert.Null(snapshot.AvailablePerDayKopecks);
    }

    [Fact]
    public void HugeAmounts_NoOverflow_ThresholdStaysExact()
    {
        // Порог считается через decimal: long·5 не переполняется.
        // Проценты готовим в decimal — limit·79 уже не влезает в long.
        long limit = long.MaxValue / 10;
        long spent79 = (long)((decimal)limit * 79m / 100m);
        long spent80 = (long)((decimal)limit * 80m / 100m);

        BudgetSnapshot ok = BudgetEngine.Compute(limit, spent79, 15, 30);
        BudgetSnapshot attention = BudgetEngine.Compute(limit, spent80, 15, 30);

        Assert.Equal(BudgetStatus.Ok, ok.Status);
        Assert.Equal(BudgetStatus.Attention, attention.Status);
    }

    [Fact]
    public void AvailablePerDay_DividesRemainderByRemainingDaysIncludingToday()
    {
        // Остаток 400,00 ₽ на 16 дней (15-е из 30 включительно) → 25,00 ₽.
        BudgetSnapshot snapshot = BudgetEngine.Compute(
            limitKopecks: 1_000_00, spentKopecks: 600_00, currentDay: 15, daysInMonth: 30);

        Assert.Equal(400_00, snapshot.RemainingKopecks);
        Assert.Equal(25_00, snapshot.AvailablePerDayKopecks);
    }

    [Fact]
    public void AvailablePerDay_OnLastDayOfMonth_IsWholeRemainder()
    {
        BudgetSnapshot snapshot = BudgetEngine.Compute(
            limitKopecks: 1_000_00, spentKopecks: 650_00, currentDay: 30, daysInMonth: 30);

        Assert.Equal(350_00, snapshot.AvailablePerDayKopecks);
    }

    [Fact]
    public void AvailablePerDay_FloorsToKopecks()
    {
        // 100 копеек на 3 дня → 33 копейки, остаток не «размазывается».
        BudgetSnapshot snapshot = BudgetEngine.Compute(
            limitKopecks: 1_00, spentKopecks: 0, currentDay: 29, daysInMonth: 31);

        Assert.Equal(33, snapshot.AvailablePerDayKopecks);
    }

    [Fact]
    public void Compute_InvalidInputs_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BudgetEngine.Compute(0, 0, 1, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BudgetEngine.Compute(-5, 0, 1, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BudgetEngine.Compute(100, -1, 1, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BudgetEngine.Compute(100, 0, 0, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BudgetEngine.Compute(100, 0, 31, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BudgetEngine.Compute(100, 0, 1, 27));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BudgetEngine.Compute(100, 0, 1, 32));
    }
}
