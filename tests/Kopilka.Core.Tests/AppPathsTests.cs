using Kopilka.Core.Storage;
using Xunit;

namespace Kopilka.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void ForDefault_UsesKopilkaDb_AndNotDemo()
    {
        AppPaths paths = AppPaths.ForDefault();

        Assert.False(paths.IsDemo);
        Assert.Equal(
            Path.Combine(AppPaths.DefaultDataDirectory(), "kopilka.db"),
            paths.DatabasePath);
    }

    [Fact]
    public void ForDemo_UsesDemoDb_AndDemoFlag()
    {
        AppPaths paths = AppPaths.ForDemo();

        Assert.True(paths.IsDemo);
        Assert.Equal(
            Path.Combine(AppPaths.DefaultDataDirectory(), "kopilka.demo.db"),
            paths.DatabasePath);
    }

    [Fact]
    public void DefaultDataDirectory_EndsWithKopilka()
    {
        string dir = AppPaths.DefaultDataDirectory();

        Assert.EndsWith("Kopilka", dir, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(dir));
    }

    [Fact]
    public void EnsureDataDirectory_CreatesDirectory_WhenMissing()
    {
        string temp = Path.Combine(Path.GetTempPath(), "kopilka-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(temp, "test.db", isDemo: false);

            bool created = paths.EnsureDataDirectory();

            Assert.True(created);
            Assert.True(Directory.Exists(temp));
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureDataDirectory_CalledTwice_Succeeds()
    {
        string temp = Path.Combine(Path.GetTempPath(), "kopilka-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(temp, "test.db", isDemo: false);

            Assert.True(paths.EnsureDataDirectory());
            Assert.True(paths.EnsureDataDirectory());
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }
}
