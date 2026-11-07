using System.Windows;
using Kopilka.App.Presentation;

namespace Kopilka.App;

/// <summary>
/// Диалог добавления транзакции. Ввод и валидация — в
/// TransactionEditViewModel; окно лишь закрывается по результату.
/// </summary>
public partial class AddTransactionWindow : Window
{
    private readonly TransactionEditViewModel _viewModel;

    public AddTransactionWindow(TransactionEditViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Save())
        {
            DialogResult = true;
        }
        // При ошибках валидации окно остаётся открытым:
        // список причин показан в самом диалоге.
    }
}
