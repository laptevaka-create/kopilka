using System.Windows;
using Kopilka.App.Presentation;
using Kopilka.Core.Data;
using Kopilka.Core.Storage;
using Kopilka.Core.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Kopilka.App;

/// <summary>
/// Точка входа приложения. Здесь собирается контейнер зависимостей:
/// источник времени, пути к данным, база SQLite и репозитории.
/// База открывается и мигрируется до показа окна: первый запуск
/// обязан закончиться готовой базой, а не пустым экраном.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool demo = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        AppPaths paths = demo ? AppPaths.ForDemo() : AppPaths.ForDefault();

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

        SqliteDatabase? database = null;
        try
        {
            database = new SqliteDatabase(paths.DatabasePath);
            new DatabaseMigrator(database.Connection).Migrate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось открыть или подготовить базу данных:\n{paths.DatabasePath}\n\n{ex.Message}",
                "Копилка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            database?.Dispose();
            Shutdown(-1);
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(paths);
        services.AddSingleton(database);
        services.AddSingleton(database.Connection);
        services.AddSingleton<IAccountRepository, SqliteAccountRepository>();
        services.AddSingleton<ICategoryRepository, SqliteCategoryRepository>();
        services.AddSingleton<ITransactionRepository, SqliteTransactionRepository>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

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
