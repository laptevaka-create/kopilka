using System.Globalization;
using Kopilka.Core.Domain;
using Kopilka.Core.Engines;

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

    /// <summary>Сумма для поля ввода: «1234,56» — без пробелов и символа валюты.</summary>
    public static string ForInput(long kopecks) =>
        (kopecks / 100m).ToString("0.00", Russian);

    /// <summary>
    /// Разбор суммы, как её вводит пользователь. Правила — в Core
    /// (<see cref="MoneyParser"/>): их используют и интерфейс, и CSV.
    /// </summary>
    public static bool TryParseRubles(string text, out long kopecks, out bool tooBig)
        => MoneyParser.TryParseRubles(text, out kopecks, out tooBig);
}
