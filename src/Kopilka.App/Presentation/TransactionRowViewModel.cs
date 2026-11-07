using Kopilka.Core.Domain;

namespace Kopilka.App.Presentation;

/// <summary>
/// Строка журнала транзакций для отображения: имена счёта и категории
/// уже подставлены, сумма отформатирована. Строка неизменяема —
/// при изменении данных список перезагружается целиком.
/// </summary>
public sealed record TransactionRowViewModel(
    long Id,
    DateOnly Date,
    string AccountName,
    string CategoryName,
    CategoryKind Kind,
    long AmountKopecks,
    string? Comment)
{
    /// <summary>Дата в привычном формате: 05.11.2026.</summary>
    public string DisplayDate => Date.ToString("dd.MM.yyyy");

    /// <summary>Сумма со знаком по типу категории: «+1 234,56 ₽» или «−1 234,56 ₽».</summary>
    public string DisplayAmount => MoneyText.FormatSigned(AmountKopecks, Kind);

    /// <summary>Комментарий без null, чтобы таблица не показывала пустые строки-ошибки.</summary>
    public string DisplayComment => Comment ?? string.Empty;
}
