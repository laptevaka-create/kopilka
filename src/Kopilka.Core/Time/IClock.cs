namespace Kopilka.Core.Time;

/// <summary>
/// Источник времени. Логика, которой нужна текущая дата, получает её
/// только через этот интерфейс — прямые вызовы DateTime.Now в движках
/// запрещены, чтобы расчёты можно было проверять на фиксированных датах.
/// </summary>
public interface IClock
{
    /// <summary>Локальное время пользователя.</summary>
    DateTime Now { get; }

    /// <summary>Время UTC.</summary>
    DateTime UtcNow { get; }
}
