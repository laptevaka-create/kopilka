using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Движок регулярных начислений: min(день, дней в месяце), границы
/// диапазона включительно, строго после lastApplied, защита входов.
/// </summary>
public class RecurringEngineTests
{
    [Fact]
    public void Day31In30DayMonth_MovesToLastDay()
    {
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            dayOfMonth: 31, lastApplied: null,
            from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 11, 30));

        DateOnly date = Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 11, 30), date);
    }

    [Fact]
    public void Day31InFebruary_LandsOn28Or29()
    {
        IReadOnlyList<DateOnly> common = RecurringEngine.GenerateDates(
            31, null, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28));
        IReadOnlyList<DateOnly> leap = RecurringEngine.GenerateDates(
            31, null, new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29));

        Assert.Equal(new DateOnly(2027, 2, 28), Assert.Single(common));
        Assert.Equal(new DateOnly(2028, 2, 29), Assert.Single(leap));
    }

    [Fact]
    public void RangeBounds_AreInclusive()
    {
        // День 10, диапазон 08–12 ноября: попадает ровно 10-е.
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            10, null, new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 12));

        Assert.Equal(new[] { new DateOnly(2026, 11, 10) }, dates);
    }

    [Fact]
    public void MultiMonthSpan_GeneratesEachMonth()
    {
        // День 5, три месяца подряд — по одному начислению в каждом.
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            5, null, new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 31));

        Assert.Equal(3, dates.Count);
        Assert.Equal(new DateOnly(2026, 11, 5), dates[0]);
        Assert.Equal(new DateOnly(2026, 12, 5), dates[1]);
        Assert.Equal(new DateOnly(2027, 1, 5), dates[2]);
    }

    [Fact]
    public void DatesStrictlyAfterLastApplied()
    {
        // LastApplied — 05.11: повторный вызов за ноябрь не даёт
        // второго начисления 5-го (ФТ-6.3).
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            5, new DateOnly(2026, 11, 5),
            from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 11, 30));

        Assert.Empty(dates);

        IReadOnlyList<DateOnly> next = RecurringEngine.GenerateDates(
            5, new DateOnly(2026, 11, 5),
            from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 12, 31));

        Assert.Equal(new[] { new DateOnly(2026, 12, 5) }, next);
    }

    [Fact]
    public void ReversedRange_ReturnsEmpty()
    {
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            5, null, new DateOnly(2026, 11, 30), new DateOnly(2026, 11, 1));

        Assert.Empty(dates);
    }

    [Fact]
    public void OccurrenceBeforeRangeStart_IsSkipped()
    {
        // День 5, диапазон с 10-го: 5-е до начала диапазона — не берётся.
        IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
            5, null, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 30));

        Assert.Empty(dates);
    }

    [Fact]
    public void InvalidDay_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecurringEngine.GenerateDates(0, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecurringEngine.GenerateDates(32, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
    }
}
