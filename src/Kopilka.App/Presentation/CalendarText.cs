using System.Globalization;

namespace Kopilka.App.Presentation;

/// <summary>
/// Русские подписи дат для заголовков окон: «Ноябрь 2026», «05.11.2026».
/// Заголовок месяца собирается в трёх окнах — правило одно, живёт
/// в одном месте.
/// </summary>
public static class CalendarText
{
    private static readonly CultureInfo Russian = new("ru-RU");

    /// <summary>«Ноябрь 2026» — месяц с заглавной, год без слова «г.».</summary>
    public static string MonthTitle(int year, int month)
    {
        string title = new DateTime(year, month, 1).ToString("MMMM yyyy", Russian);
        return char.ToUpper(title[0], Russian) + title[1..];
    }

    /// <summary>«05.11.2026» — дата без времени.</summary>
    public static string FormatDate(DateOnly date) => date.ToString("dd.MM.yyyy", Russian);
}
