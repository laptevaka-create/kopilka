namespace Kopilka.Core.Domain;

/// <summary>
/// Настройка вида «ключ—значение»: счёт по умолчанию для новых
/// транзакций и прочие параметры приложения.
/// </summary>
public sealed class Setting
{
    /// <summary>Ключ настройки, первичный ключ.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Значение настройки.</summary>
    public string Value { get; set; } = string.Empty;
}
