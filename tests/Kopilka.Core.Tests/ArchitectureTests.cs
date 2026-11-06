using System.Text.RegularExpressions;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Архитектурные проверки: требования к разделению проектов,
/// заявленные в шаге 0, фиксируются тестом, а не только словами.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void Core_TargetFramework_IsPortableNet()
    {
        string csproj = ReadCoreCsproj();

        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", csproj);
        Assert.DoesNotContain("net10.0-windows", csproj);
        Assert.DoesNotContain("UseWPF", csproj);
    }

    [Fact]
    public void AllProjects_UseOnlyAllowedPackages()
    {
        // Пакетная дисциплина проекта: только SQLite, Microsoft.Extensions
        // и xunit. Проверяется по всем csproj сразу — меньше шансов, что
        // «одна маленькая библиотечка» просочится незамеченной.
        string root = FindRepoRoot();
        string[] csprojs =
        {
            Path.Combine(root, "src", "Kopilka.Core", "Kopilka.Core.csproj"),
            Path.Combine(root, "src", "Kopilka.App", "Kopilka.App.csproj"),
            Path.Combine(root, "tests", "Kopilka.Core.Tests", "Kopilka.Core.Tests.csproj"),
        };

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Microsoft.Data.Sqlite",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Logging.Debug",
            "Microsoft.NET.Test.Sdk",
            "xunit",
            "xunit.runner.visualstudio",
        };

        foreach (string csproj in csprojs)
        {
            Assert.True(File.Exists(csproj), $"Файл не найден: {csproj}");
            foreach (Match match in Regex.Matches(File.ReadAllText(csproj), @"PackageReference\s+Include=""([^""]+)"""))
            {
                string package = match.Groups[1].Value;
                Assert.True(allowed.Contains(package),
                    $"Пакет вне разрешённого списка: {package} ({Path.GetFileName(csproj)})");
            }
        }
    }

    private static string ReadCoreCsproj()
    {
        string repoRoot = FindRepoRoot();
        string path = Path.Combine(repoRoot, "src", "Kopilka.Core", "Kopilka.Core.csproj");
        Assert.True(File.Exists(path), $"Файл не найден: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kopilka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Корень репозитория (Kopilka.sln) не найден");
        return dir!.FullName;
    }
}
