using System.Globalization;

namespace Kopilka.Core.Engines;

/// <summary>
/// Разбор суммы, введённой текстом: рубли с запятой (точка считается
/// опечаткой запятой), пробелы-разделители тысяч допустимы.
/// Используется и интерфейсом, и валидатором CSV — одинаковые правила.
/// </summary>
public static class MoneyParser
{
    private static readonly CultureInfo Russian = new("ru-RU");

    /// <summary>
    /// Рубли из текста → копейки. Возвращает false, если текст не число;
    /// tooBig — если сумма не помещается в копейки long.
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
