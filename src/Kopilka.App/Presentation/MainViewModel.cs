using System.Collections.ObjectModel;
using System.Globalization;
using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Kopilka.Core.Time;

namespace Kopilka.App.Presentation;

/// <summary>
/// Главная модель представления: журнал транзакций выбранного месяца
/// с итогами. Не знает о View: данные берёт из репозиториев, диалоги
/// показывает через IDialogService. Тестируется без интерфейса.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly ITransactionRepository _transactions;
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly IDialogService _dialogs;
    private readonly IClock _clock;

    private int _year;
    private int _month;
    private string _monthTitle = string.Empty;
    private long _incomeKopecks;
    private long _expenseKopecks;

    public MainViewModel(
        ITransactionRepository transactions,
        IAccountRepository accounts,
        ICategoryRepository categories,
        IDialogService dialogs,
        IClock clock)
    {
        _transactions = transactions;
        _accounts = accounts;
        _categories = categories;
        _dialogs = dialogs;
        _clock = clock;

        Transactions = new ObservableCollection<TransactionRowViewModel>();
        PreviousMonthCommand = new RelayCommand(GoToPreviousMonth);
        NextMonthCommand = new RelayCommand(GoToNextMonth);
        AddTransactionCommand = new RelayCommand(AddTransaction);

        _year = clock.Now.Year;
        _month = clock.Now.Month;
        Reload();
    }

    /// <summary>Строки журнала текущего месяца, отсортированные по дате.</summary>
    public ObservableCollection<TransactionRowViewModel> Transactions { get; }

    /// <summary>Заголовок месяца: «Ноябрь 2026».</summary>
    public string MonthTitle
    {
        get => _monthTitle;
        private set => SetProperty(ref _monthTitle, value);
    }

    /// <summary>Сумма доходов месяца в копейках.</summary>
    public long IncomeKopecks
    {
        get => _incomeKopecks;
        private set
        {
            if (SetProperty(ref _incomeKopecks, value))
            {
                RaisePropertyChanged(nameof(IncomeText));
                RaisePropertyChanged(nameof(BalanceText));
            }
        }
    }

    /// <summary>Сумма расходов месяца в копейках.</summary>
    public long ExpenseKopecks
    {
        get => _expenseKopecks;
        private set
        {
            if (SetProperty(ref _expenseKopecks, value))
            {
                RaisePropertyChanged(nameof(ExpenseText));
                RaisePropertyChanged(nameof(BalanceText));
            }
        }
    }

    /// <summary>Доходы месяца для отображения.</summary>
    public string IncomeText => MoneyText.Format(IncomeKopecks);

    /// <summary>Расходы месяца для отображения.</summary>
    public string ExpenseText => MoneyText.Format(ExpenseKopecks);

    /// <summary>Сальдо месяца: доходы минус расходы.</summary>
    public long BalanceKopecks => IncomeKopecks - ExpenseKopecks;

    /// <summary>Сальдо для отображения, с минусом при отрицательном.</summary>
    public string BalanceText => MoneyText.Format(BalanceKopecks);

    public RelayCommand PreviousMonthCommand { get; }

    public RelayCommand NextMonthCommand { get; }

    public RelayCommand AddTransactionCommand { get; }

    /// <summary>Перезагружает журнал и итоги за выбранный месяц.</summary>
    public void Reload()
    {
        DateOnly from = new(_year, _month, 1);
        DateOnly to = new(_year, _month, DateTime.DaysInMonth(_year, _month));

        Dictionary<long, string> accountNames = _accounts.ListAll()
            .ToDictionary(a => a.Id, a => a.Name);
        Dictionary<long, Category> categoriesById = _categories.ListAll()
            .ToDictionary(c => c.Id);

        IReadOnlyList<Transaction> monthTransactions = _transactions.ListByPeriod(from, to);

        Transactions.Clear();
        long income = 0;
        long expense = 0;
        foreach (Transaction transaction in monthTransactions)
        {
            Category category = categoriesById[transaction.CategoryId];
            Transactions.Add(new TransactionRowViewModel(
                transaction.Id,
                transaction.Date,
                accountNames[transaction.AccountId],
                category.Name,
                category.Kind,
                transaction.AmountKopecks,
                transaction.Comment));

            if (category.Kind == CategoryKind.Income)
            {
                income += transaction.AmountKopecks;
            }
            else
            {
                expense += transaction.AmountKopecks;
            }
        }

        IncomeKopecks = income;
        ExpenseKopecks = expense;
        MonthTitle = TitleOf(_year, _month);
    }

    private void GoToPreviousMonth()
    {
        _month--;
        if (_month < 1)
        {
            _month = 12;
            _year--;
        }

        Reload();
    }

    private void GoToNextMonth()
    {
        _month++;
        if (_month > 12)
        {
            _month = 1;
            _year++;
        }

        Reload();
    }

    private void AddTransaction()
    {
        if (_dialogs.ShowAddTransaction())
        {
            Reload();
        }
    }

    private static string TitleOf(int year, int month)
    {
        string title = new DateTime(year, month, 1).ToString("MMMM yyyy", new CultureInfo("ru-RU"));
        return char.ToUpper(title[0], CultureInfo.CurrentCulture) + title[1..];
    }
}
