namespace Kopilka.Core.Engines;

/// <summary>
/// Движок регулярных начислений — чистая функция: по дню месяца
/// и диапазону возвращает даты начислений, строго после LastApplied.
/// Дата начисления в месяце = min(день, дней в месяце): 31-е
/// в 30-дневном месяце относится к 30-му, в феврале — к 28-му/29-му.
/// Правила нормированы в docs/specs/finance-rules.md.
/// </summary>
public static class RecurringEngine
{
    /// <summary>
    /// Даты начислений в диапазоне [from, to], каждая строго после
    /// lastApplied. Пустой или перевёрнутый диапазон — пустой список.
    /// </summary>
    public static IReadOnlyList<DateOnly> GenerateDates(
        int dayOfMonth, DateOnly? lastApplied, DateOnly from, DateOnly to)
    {
        if (dayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dayOfMonth), dayOfMonth, "День месяца — от 1 до 31.");
        }

        if (from > to)
        {
            return Array.Empty<DateOnly>();
        }

        var dates = new List<DateOnly>();
        int year = from.Year;
        int month = from.Month;

        while (year * 12 + month <= to.Year * 12 + to.Month)
        {
            int day = Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month));
            var date = new DateOnly(year, month, day);

            if (date >= from
                && date <= to
                && (lastApplied is null || date > lastApplied))
            {
                dates.Add(date);
            }

            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }

        return dates;
    }
}
