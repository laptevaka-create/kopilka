namespace Kopilka.Core.Domain;

/// <summary>
/// Счёт: «Наличные», «Карта» или собственный. Показывает, где лежат деньги.
/// Счёт по умолчанию для новых транзакций хранится в настройках,
/// а не флагом здесь: один факт — одно место.
/// </summary>
public sealed class Account
{
    /// <summary>Идентификатор счёта.</summary>
    public long Id { get; set; }

    /// <summary>Имя счёта, уникально.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Порядок отображения в списках.</summary>
    public int SortOrder { get; set; }
}
