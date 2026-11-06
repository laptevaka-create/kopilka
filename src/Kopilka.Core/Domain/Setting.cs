namespace Kopilka.Core.Domain;

/// <summary>
/// Настройка вида «ключ—значение»: счёт по умолчанию для новых
/// транзакций и прочие параметры приложения.
/// </summary>
public sealed record Setting(
    string Key,
    string Value);
