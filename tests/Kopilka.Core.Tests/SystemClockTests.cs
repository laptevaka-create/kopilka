using Kopilka.Core.Time;
using Xunit;

namespace Kopilka.Core.Tests;

public class SystemClockTests
{
    [Fact]
    public void Now_IsNotDefaultValue()
    {
        IClock clock = new SystemClock();

        Assert.NotEqual(default, clock.Now);
    }

    [Fact]
    public void UtcNow_HasUtcKind()
    {
        IClock clock = new SystemClock();

        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }

    [Fact]
    public void UtcNow_AndNow_DifferByLessThanDay()
    {
        // Часовой пояс пользователя может быть любым, но не больше суток.
        IClock clock = new SystemClock();

        TimeSpan difference = clock.Now - clock.UtcNow;

        Assert.True(Math.Abs(difference.TotalHours) < 24,
            $"Разница Now и UtcNow превысила сутки: {difference}");
    }
}
