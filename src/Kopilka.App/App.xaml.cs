using System.Windows;
using Kopilka.App.Presentation;
using Kopilka.Core.Data;
using Kopilka.Core.Storage;
using Kopilka.Core.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kopilka.App;

/// <summary>
/// Точка входа приложения. Здесь собирается контейнер зависимостей:
/// источник времени, пути к данным, фабрика контекста EF Core
/// и репозитории. База создаётся и мигрируется до показа окна:
/// первый запуск обязан закончиться готовой базой, а не пустым экраном.
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

        try
        {
            // WAL хранится в самом файле базы: включаем один раз,
            // дальше режим переживает любые соединения (включая EF).
            EnableWal(paths.DatabasePath);

            // Создание схемы и применение миграций EF Core.
            DbContextOptions<KopilkaDbContext> options = new DbContextOptionsBuilder<KopilkaDbContext>()
                .UseSqlite($"Data Source={paths.DatabasePath}")
                .Options;
            using (var context = new KopilkaDbContext(options))
            {
                context.Database.Migrate();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось открыть или подготовить базу данных:\n{paths.DatabasePath}\n\n{ex.Message}",
                "Копилка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(paths);
        services.AddDbContextFactory<KopilkaDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabasePath}"));
        services.AddSingleton<IAccountRepository, EfAccountRepository>();
        services.AddSingleton<ICategoryRepository, EfCategoryRepository>();
        services.AddSingleton<ITransactionRepository, EfTransactionRepository>();
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

    /// <summary>Включает журнальный режим WAL: целостность базы при сбое питания.</summary>
    private static void EnableWal(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        command.ExecuteNonQuery();
    }
}
