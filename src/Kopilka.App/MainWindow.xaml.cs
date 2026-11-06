using System.Windows;
using Kopilka.Core.Storage;

namespace Kopilka.App;

/// <summary>
/// Главное окно. На шаге 1 показывает состояние хранилища;
/// журнал транзакций и модели представления появятся на шаге 2.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(AppPaths paths)
    {
        InitializeComponent();

        string demoSuffix = paths.IsDemo ? " (демо-режим)" : string.Empty;
        DatabaseStatusText.Text = $"База данных: {paths.DatabasePath}{demoSuffix}";
    }
}
