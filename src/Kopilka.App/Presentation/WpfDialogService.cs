using System.Windows;
using Kopilka.Core.Data;
using Kopilka.Core.Time;

namespace Kopilka.App.Presentation;

/// <summary>
/// WPF-реализация сервиса диалогов: показывает окно добавления
/// транзакции. Модели представления знают только интерфейс,
/// поэтому в тестах сервис подменяется.
/// </summary>
public sealed class WpfDialogService : IDialogService
{
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly ITransactionRepository _transactions;
    private readonly IClock _clock;

    public WpfDialogService(
        IAccountRepository accounts,
        ICategoryRepository categories,
        ITransactionRepository transactions,
        IClock clock)
    {
        _accounts = accounts;
        _categories = categories;
        _transactions = transactions;
        _clock = clock;
    }

    public bool ShowAddTransaction()
    {
        var viewModel = new TransactionEditViewModel(
            _accounts, _categories, _transactions, _clock);
        var window = new AddTransactionWindow(viewModel)
        {
            Owner = Application.Current?.MainWindow,
        };

        return window.ShowDialog() == true;
    }
}
