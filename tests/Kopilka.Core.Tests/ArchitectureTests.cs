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
    public void Core_HasNoPackageReferences()
    {
        // Ядро не зависит от внешних библиотек: SQLite и DI появятся
        // только там, где реально используются, и это фиксируется здесь.
        string csproj = ReadCoreCsproj();

        Assert.DoesNotContain("PackageReference", csproj);
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
