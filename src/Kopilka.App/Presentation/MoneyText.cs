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
}
