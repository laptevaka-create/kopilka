using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контрактные границы генерации регулярных начислений по нормативу
/// finance-rules.md: переход 31-го через февраль, строго-после
/// lastApplied внутри диапазона, однодневные диапазоны и порядок дат.
/// </summary>
public class RecurringEngineContractTests
{
    [Fact]
    public void GenerateDates_Day31ThroughJanuaryFebruaryMarch_CommonYear_ClampsTo28AndRestores31()
    {
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            31, null, new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 31));

        Assert.Equal(
            new[] { new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31) },
            dates);
    }

    [Fact]
    public void GenerateDates_Day31ThroughJanuaryFebruaryMarch_LeapYear_ClampsTo29AndRestores31()
    {
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            31, null, new DateOnly(2028, 1, 1), new DateOnly(2028, 3, 31));

        Assert.Equal(
            new[] { new DateOnly(2028, 1, 31), new DateOnly(2028, 2, 29), new DateOnly(2028, 3, 31) },
            dates);
    }

    [Fact]
    public void GenerateDates_LastAppliedInsideRange_ExcludesUpToItAndGeneratesAfter()
    {
        // lastApplied совпадает с декабрьским начислением: ноябрьское и
        // декабрьское исключены, январское — строго после и попадает.
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            15, new DateOnly(2026, 12, 15),
            new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 31));

        Assert.Equal(new[] { new DateOnly(2027, 1, 15) }, dates);
    }

    [Fact]
    public void GenerateDates_LastAppliedAtRangeEnd_ReturnsEmpty()
    {
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            15, new DateOnly(2027, 1, 15),
            new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 31));

        Assert.Empty(dates);
    }

    [Fact]
    public void GenerateDates_SingleDayRangeOnAccrualDate_ReturnsOneDate()
    {
        var day = new DateOnly(2026, 11, 15);

        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(15, null, day, day);

        Assert.Equal(day, Assert.Single(dates));
    }

    [Fact]
    public void GenerateDates_SingleDayRangeBeforeAccrualDate_ReturnsEmpty()
    {
        var day = new DateOnly(2026, 11, 14);

        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(15, null, day, day);

        Assert.Empty(dates);
    }

    [Fact]
    public void GenerateDates_SingleDayRangeEqualLastApplied_ReturnsEmpty()
    {
        var day = new DateOnly(2026, 11, 15);

        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(15, day, day, day);

        Assert.Empty(dates);
    }

    [Fact]
    public void GenerateDates_Day1AndDay15InSameRange_ReturnsBothInAscendingOrder()
    {
        var from = new DateOnly(2026, 11, 1);
        var to = new DateOnly(2026, 11, 30);

        IReadOnlyList<DateOnly> first = RecurringEngine.GenerateDates(1, null, from, to);
        IReadOnlyList<DateOnly> fifteenth = RecurringEngine.GenerateDates(15, null, from, to);

        DateOnly firstDate = Assert.Single(first);
        DateOnly fifteenthDate = Assert.Single(fifteenth);
        Assert.Equal(new DateOnly(2026, 11, 1), firstDate);
        Assert.Equal(new DateOnly(2026, 11, 15), fifteenthDate);
        Assert.True(firstDate < fifteenthDate);
    }

    [Fact]
    public void GenerateDates_Day29InFebruary_CommonAndLeapYear_ClampsOnlyInCommonYear()
    {
        IReadOnlyList<DateOnly> common = RecurringEngine.GenerateDates(
            29, null, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28));
        IReadOnlyList<DateOnly> leap = RecurringEngine.GenerateDates(
            29, null, new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29));

        Assert.Equal(new DateOnly(2027, 2, 28), Assert.Single(common));
        Assert.Equal(new DateOnly(2028, 2, 29), Assert.Single(leap));
    }

    [Fact]
    public void GenerateDates_Day30InFebruary_CommonAndLeapYear_ClampsToLastDay()
    {
        IReadOnlyList<DateOnly> common = RecurringEngine.GenerateDates(
            30, null, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28));
        IReadOnlyList<DateOnly> leap = RecurringEngine.GenerateDates(
            30, null, new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29));

        Assert.Equal(new DateOnly(2027, 2, 28), Assert.Single(common));
        Assert.Equal(new DateOnly(2028, 2, 29), Assert.Single(leap));
    }
}
