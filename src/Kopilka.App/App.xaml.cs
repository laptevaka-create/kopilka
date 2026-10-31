using System.Windows;
using Kopilka.Core.Storage;
using Kopilka.Core.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Kopilka.App;

/// <summary>
/// Точка входа приложения. Здесь собирается контейнер зависимостей:
/// источник времени, пути к данным (обычные или демо по ключу --demo)
/// и главное окно. Дальше по цепочке все классы получают зависимости
/// через конструкторы.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool demo = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase);

        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(demo ? AppPaths.ForDemo() : AppPaths.ForDefault());
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        AppPaths paths = _services.GetRequiredService<AppPaths>();
        if (!paths.EnsureDataDirectory())
        {
            MessageBox.Show(
                $"Не удалось создать каталог данных:\n{paths.DataDirectory}",
                "Копилка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        MainWindow window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
