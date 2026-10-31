using System.Windows;

namespace Kopilka.App;

/// <summary>
/// Главное окно. На шаге 0 это пустой каркас; содержимое и привязки данных
/// появятся на шаге 2 вместе с журналом транзакций и моделями представления.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
