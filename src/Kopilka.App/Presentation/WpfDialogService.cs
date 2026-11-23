using System.Windows;
using Kopilka.Core.Data;
using Kopilka.Core.Time;

namespace Kopilka.App.Presentation;

/// <summary>
/// WPF-реализация сервиса диалогов: окна добавления и редактирования
/// транзакции, справочник категорий, подтверждение. Модели представления
/// знают только интерфейс, поэтому в тестах сервис подменяется.
/// </summary>
public sealed class WpfDialogService : IDialogService
{
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly ITransactionRepository _transactions;
    private readonly IBudgetRepository _budgets;
    private readonly IClock _clock;

    public WpfDialogService(
        IAccountRepository accounts,
        ICategoryRepository categories,
        ITransactionRepository transactions,
        IBudgetRepository budgets,
        IClock clock)
    {
        _accounts = accounts;
        _categories = categories;
        _transactions = transactions;
        _budgets = budgets;
        _clock = clock;
    }

    public bool ShowAddTransaction()
    {
        var viewModel = new TransactionEditViewModel(
            _accounts, _categories, _transactions, _clock);

        return ShowTransactionDialog(viewModel);
    }

    public bool ShowEditTransaction(long transactionId)
    {
        Kopilka.Core.Domain.Transaction? transaction = _transactions.GetById(transactionId);
        if (transaction is null)
        {
            // Удалили, пока строка была выбрана: главная модель
            // сама перечитает журнал.
            return false;
        }

        var viewModel = new TransactionEditViewModel(
            _accounts, _categories, _transactions, _clock);
        viewModel.BeginEdit(transaction);

        return ShowTransactionDialog(viewModel);
    }

    public void ShowCategories()
    {
        var viewModel = new CategoriesViewModel(_categories);
        var window = new CategoriesWindow(viewModel)
        {
            Owner = Application.Current?.MainWindow,
        };
        window.ShowDialog();
    }

    public void ShowBudgets(string month)
    {
        var viewModel = new BudgetsViewModel(_categories, _budgets, month);
        var window = new BudgetsWindow(viewModel)
        {
            Owner = Application.Current?.MainWindow,
        };
        window.ShowDialog();
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

    private static bool ShowTransactionDialog(TransactionEditViewModel viewModel)
    {
        var window = new AddTransactionWindow(viewModel)
        {
            Owner = Application.Current?.MainWindow,
        };

        return window.ShowDialog() == true;
    }
}
