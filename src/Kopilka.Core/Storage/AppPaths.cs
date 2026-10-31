namespace Kopilka.Core.Storage;

/// <summary>
/// Пути к данным приложения. База живёт в %LOCALAPPDATA%\Kopilka.
/// Демо-режим (--demo) использует отдельный файл, чтобы не трогать
/// реальные данные пользователя.
/// </summary>
public sealed class AppPaths
{
    /// <summary>Каталог данных, %LOCALAPPDATA%\Kopilka.</summary>
    public string DataDirectory { get; }

    /// <summary>Полный путь к файлу базы данных.</summary>
    public string DatabasePath { get; }

    /// <summary>Признак демо-режима.</summary>
    public bool IsDemo { get; }

    /// <summary>
    /// Создаёт описание путей. Каталог на диске не создаётся —
    /// это делает <see cref="EnsureDataDirectory"/>.
    /// </summary>
    /// <param name="dataDirectory">Каталог данных.</param>
    /// <param name="databaseFileName">Имя файла базы в этом каталоге.</param>
    /// <param name="isDemo">Демо-режим.</param>
    public AppPaths(string dataDirectory, string databaseFileName, bool isDemo)
    {
        DataDirectory = dataDirectory;
        DatabasePath = Path.Combine(dataDirectory, databaseFileName);
        IsDemo = isDemo;
    }

    /// <summary>Обычный режим: kopilka.db в %LOCALAPPDATA%\Kopilka.</summary>
    public static AppPaths ForDefault()
    {
        string dir = DefaultDataDirectory();
        return new AppPaths(dir, "kopilka.db", isDemo: false);
    }

    /// <summary>Демо-режим: kopilka.demo.db в том же каталоге.</summary>
    public static AppPaths ForDemo()
    {
        string dir = DefaultDataDirectory();
        return new AppPaths(dir, "kopilka.demo.db", isDemo: true);
    }

    /// <summary>Каталог данных по умолчанию: %LOCALAPPDATA%\Kopilka.</summary>
    public static string DefaultDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kopilka");

    /// <summary>
    /// Создаёт каталог данных, если его нет. Возвращает false,
    /// если каталог недоступен для записи.
    /// </summary>
    public bool EnsureDataDirectory()
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
