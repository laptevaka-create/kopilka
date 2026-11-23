using System.Globalization;
using Kopilka.Core.Domain;

namespace Kopilka.App.Presentation;

/// <summary>
/// Форматирование сумм для интерфейса: копейки переводятся в рубли
/// только здесь, на выводе. Все расчёты и хранение — в копейках,
/// поэтому суммирование не накапливает ошибок округления.
/// </summary>
public static class MoneyText
{
    private static readonly CultureInfo Russian = new("ru-RU");

    /// <summary>«1 234,56 ₽» — для итогов и сальдо, с минусом при отрицательном.</summary>
    public static string Format(long kopecks)
    {
        decimal rubles = kopecks / 100m;
        return string.Format(Russian, "{0:N2} ₽", rubles);
    }

    /// <summary>«+1 234,56 ₽» для дохода, «−1 234,56 ₽» для расхода.</summary>
    public static string FormatSigned(long kopecks, CategoryKind kind)
    {
        string sign = kind == CategoryKind.Income ? "+" : "−";
        return sign + Format(kopecks);
    }

    /// <summary>
    /// Разбор суммы, как её вводит пользователь: рубли с запятой
    /// (точка считается опечаткой запятой), пробелы-разделители тысяч
    /// допустимы. Возвращает false, если текст не число; tooBig —
    /// если число не помещается в копейки long.
    /// </summary>
    public static bool TryParseRubles(string text, out long kopecks, out bool tooBig)
    {
        tooBig = false;
        string normalized = text.Trim().Replace('.', ',');
        if (!decimal.TryParse(normalized, NumberStyles.Number, Russian, out decimal rubles))
        {
            kopecks = 0;
            return false;
        }

        decimal rounded = Math.Round(rubles * 100m, MidpointRounding.AwayFromZero);
        if (rounded > long.MaxValue || rounded < long.MinValue)
        {
            // Столько копеек не помещается в long: отказ с причиной,
            // а не падение при приведении типа.
            kopecks = 0;
            tooBig = true;
            return false;
        }

        kopecks = (long)rounded;
        return true;
    }
}
