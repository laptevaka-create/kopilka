using System.Windows;
using Kopilka.App.Presentation;
using Kopilka.Core.Storage;

namespace Kopilka.App;

/// <summary>
/// Главное окно. Вся логика — в MainViewModel, окно показывает её
/// через привязки; статусная строка хранилища — служебный текст
/// (путь к базе), он не относится к бизнес-логике.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel, AppPaths paths)
    {
        InitializeComponent();

        DataContext = viewModel;

        string demoSuffix = paths.IsDemo ? " (демо-режим)" : string.Empty;
        DatabaseStatusText.Text = $"База данных: {paths.DatabasePath}{demoSuffix}";
    }

    /// <summary>Двойной клик по строке журнала — редактирование транзакции.</summary>
    private void OnTransactionDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel
            && viewModel.EditTransactionCommand.CanExecute(null))
        {
            viewModel.EditTransactionCommand.Execute(null);
        }
    }
}
