using Kopilka.Core.Domain;

namespace Kopilka.Core.Engines;

/// <summary>Статус исполнения месячного бюджета.</summary>
public enum BudgetStatus
{
    /// <summary>Потрачено меньше 80 % лимита.</summary>
    Ok,

    /// <summary>Потрачено от 80 % лимита включительно, но меньше лимита.</summary>
    Attention,

    /// <summary>Потрачено не меньше лимита.</summary>
    Exceeded,
}

/// <summary>
/// Снимок бюджета категории на месяц: статус, остаток и «доступно
/// в день». «Доступно в день» есть только у статусов «в норме»
/// и «внимание» — у превышенного бюджета остатка нет.
/// </summary>
public sealed record BudgetSnapshot(
    BudgetStatus Status,
    long RemainingKopecks,
    long? AvailablePerDayKopecks);

/// <summary>
/// Движок статусов бюджета — чистая функция: входы → выход, без базы
/// и без UI. Правила нормированы в docs/specs/finance-rules.md.
/// Все суммы — целые копейки, порог 80 % считается без плавающей
/// точки, поэтому границы точные на любых величинах.
/// </summary>
public static class BudgetEngine
{
    /// <summary>
    /// Вычисляет статус бюджета, остаток и «доступно в день».
    /// Текущий день считается оставшимся: в последний день месяца
    /// «доступно в день» равно остатку целиком.
    /// </summary>
    public static BudgetSnapshot Compute(
        long limitKopecks, long spentKopecks, int currentDay, int daysInMonth)
    {
        if (limitKopecks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limitKopecks), limitKopecks, "Лимит бюджета положительный.");
        }

        if (spentKopecks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spentKopecks), spentKopecks, "Потрачено неотрицательно.");
        }

        if (daysInMonth is < 28 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(daysInMonth), daysInMonth, "В месяце 28–31 день.");
        }

        if (currentDay < 1 || currentDay > daysInMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentDay), currentDay, "Текущий день — внутри месяца.");
        }

        // Порог 80 % без дробей: spent/limit >= 4/5 ⇔ spent·5 >= limit·4.
        // decimal промежуточный: long·5 не переполняется в decimal
        // (decimal держит до ~7,9·10^28, long·5 ≤ ~4,6·10^19).
        decimal spentTimes5 = (decimal)spentKopecks * 5m;
        decimal limitTimes4 = (decimal)limitKopecks * 4m;

        BudgetStatus status =
            spentKopecks >= limitKopecks ? BudgetStatus.Exceeded
            : spentTimes5 >= limitTimes4 ? BudgetStatus.Attention
            : BudgetStatus.Ok;

        long remaining = limitKopecks - spentKopecks;
        long? availablePerDay = status == BudgetStatus.Exceeded
            ? null
            : remaining / (daysInMonth - currentDay + 1);

        return new BudgetSnapshot(status, remaining, availablePerDay);
    }
}
