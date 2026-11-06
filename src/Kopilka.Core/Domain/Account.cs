namespace Kopilka.Core.Domain;

/// <summary>
/// Счёт: «Наличные», «Карта» или собственный. Показывает, где лежат деньги.
/// Счёт по умолчанию для новых транзакций хранится в настройках,
/// а не флагом здесь: один факт — одно место.
/// </summary>
public sealed record Account(
    long Id,
    string Name,
    int SortOrder);
