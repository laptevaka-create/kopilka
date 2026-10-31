namespace Kopilka.Core.Time;

/// <summary>
/// Системное время: реализация для приложения.
/// В тестах движков вместо неё используется фиксированное время.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;

    public DateTime UtcNow => DateTime.UtcNow;
}
