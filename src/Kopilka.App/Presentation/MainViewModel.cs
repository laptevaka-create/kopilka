using System.Collections.ObjectModel;
using System.Globalization;
using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Kopilka.Core.Time;

namespace Kopilka.App.Presentation;

/// <summary>
/// Главная модель представления: журнал транзакций с итогами.
/// Журнал показывает выбранный месяц либо произвольный период
/// (когда задан фильтр дат «с — по»); оба режима комбинируются
/// с фильтром по категории. Итоги пересчитываются по видимым строкам.
/// Не знает о View: данные из репозиториев, диалоги через IDialogService.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private static readonly CultureInfo Russian = new("ru-RU");

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

    private FilterCategoryOption? _selectedFilterCategory;
    private string _filterFromText = string.Empty;
    private string _filterToText = string.Empty;
    private string _filterError = string.Empty;
    private bool _isCustomPeriod;
    private DateOnly _periodFrom;
    private DateOnly _periodTo;
    private TransactionRowViewModel? _selectedTransaction;

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
        FilterCategories = new ObservableCollection<FilterCategoryOption>();

        PreviousMonthCommand = new RelayCommand(GoToPreviousMonth, () => !IsCustomPeriod);
        NextMonthCommand = new RelayCommand(GoToNextMonth, () => !IsCustomPeriod);
        AddTransactionCommand = new RelayCommand(AddTransaction);
        EditTransactionCommand = new RelayCommand(EditTransaction, () => SelectedTransaction is not null);
        DeleteTransactionCommand = new RelayCommand(DeleteTransaction, () => SelectedTransaction is not null);
        ShowCategoriesCommand = new RelayCommand(ShowCategories);
        ApplyPeriodCommand = new RelayCommand(ApplyPeriod);
        ResetPeriodCommand = new RelayCommand(ResetPeriod);

        _year = clock.Now.Year;
        _month = clock.Now.Month;

        RebuildFilterCategories();
        Reload();
    }

    /// <summary>Строки журнала текущего периода, отсортированные по дате.</summary>
    public ObservableCollection<TransactionRowViewModel> Transactions { get; }

    /// <summary>Варианты фильтра по категории; первый — «Все категории».</summary>
    public ObservableCollection<FilterCategoryOption> FilterCategories { get; }

    /// <summary>Заголовок периода: «Ноябрь 2026» или «Период: 05.11.2026 — 20.11.2026».</summary>
    public string MonthTitle
    {
        get => _monthTitle;
        private set => SetProperty(ref _monthTitle, value);
    }

    /// <summary>Сумма доходов видимых строк в копейках.</summary>
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

    /// <summary>Сумма расходов видимых строк в копейках.</summary>
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

    /// <summary>Доходы для отображения.</summary>
    public string IncomeText => MoneyText.Format(IncomeKopecks);

    /// <summary>Расходы для отображения.</summary>
    public string ExpenseText => MoneyText.Format(ExpenseKopecks);

    /// <summary>Сальдо видимых строк: доходы минус расходы.</summary>
    public long BalanceKopecks => IncomeKopecks - ExpenseKopecks;

    /// <summary>Сальдо для отображения, с минусом при отрицательном.</summary>
    public string BalanceText => MoneyText.Format(BalanceKopecks);

    /// <summary>Выбранная в фильтре категория; null-вариант — «Все категории».</summary>
    public FilterCategoryOption? SelectedFilterCategory
    {
        get => _selectedFilterCategory;
        set
        {
            if (SetProperty(ref _selectedFilterCategory, value))
            {
                Reload();
            }
        }
    }

    /// <summary>Начало произвольного периода, как введено: «05.11.2026».</summary>
    public string FilterFromText
    {
        get => _filterFromText;
        set => SetProperty(ref _filterFromText, value);
    }

    /// <summary>Конец произвольного периода, как введено: «20.11.2026».</summary>
    public string FilterToText
    {
        get => _filterToText;
        set => SetProperty(ref _filterToText, value);
    }

    /// <summary>Ошибка фильтра периода, пусто — если период корректен.</summary>
    public string FilterError
    {
        get => _filterError;
        private set => SetProperty(ref _filterError, value);
    }

    /// <summary>
    /// true, когда журнал показывает произвольный период, а не месяц.
    /// В этом режиме навигация «‹ ›» отключена: сдвигать месяц,
    /// которого на экране нет, бессмысленно.
    /// </summary>
    public bool IsCustomPeriod
    {
        get => _isCustomPeriod;
        private set
        {
            if (SetProperty(ref _isCustomPeriod, value))
            {
                PreviousMonthCommand.RaiseCanExecuteChanged();
                NextMonthCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Выбранная строка журнала; включает команды изменения и удаления.</summary>
    public TransactionRowViewModel? SelectedTransaction
    {
        get => _selectedTransaction;
        set
        {
            if (SetProperty(ref _selectedTransaction, value))
            {
                EditTransactionCommand.RaiseCanExecuteChanged();
                DeleteTransactionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand PreviousMonthCommand { get; }

    public RelayCommand NextMonthCommand { get; }

    public RelayCommand AddTransactionCommand { get; }

    public RelayCommand EditTransactionCommand { get; }

    public RelayCommand DeleteTransactionCommand { get; }

    public RelayCommand ShowCategoriesCommand { get; }

    public RelayCommand ApplyPeriodCommand { get; }

    public RelayCommand ResetPeriodCommand { get; }

    /// <summary>Перезагружает журнал и итоги за текущий период с учётом фильтров.</summary>
    public void Reload()
    {
        DateOnly from;
        DateOnly to;
        if (IsCustomPeriod)
        {
            from = _periodFrom;
            to = _periodTo;
        }
        else
        {
            from = new DateOnly(_year, _month, 1);
            to = new DateOnly(_year, _month, DateTime.DaysInMonth(_year, _month));
        }

        Dictionary<long, string> accountNames = _accounts.ListAll()
            .ToDictionary(a => a.Id, a => a.Name);
        Dictionary<long, Category> categoriesById = _categories.ListAll()
            .ToDictionary(c => c.Id);

        IReadOnlyList<Transaction> periodTransactions = _transactions.ListByPeriod(
            from, to, SelectedFilterCategory?.Id);

        Transactions.Clear();
        long income = 0;
        long expense = 0;
        foreach (Transaction transaction in periodTransactions)
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
        MonthTitle = IsCustomPeriod
            ? $"Период: {FormatDate(_periodFrom)} — {FormatDate(_periodTo)}"
            : TitleOf(_year, _month);
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

    private void EditTransaction()
    {
        if (SelectedTransaction is null)
        {
            return;
        }

        if (_dialogs.ShowEditTransaction(SelectedTransaction.Id))
        {
            Reload();
        }
    }

    private void DeleteTransaction()
    {
        if (SelectedTransaction is null)
        {
            return;
        }

        bool confirmed = _dialogs.Confirm(
            "Удалить транзакцию",
            $"Удалить транзакцию от {FormatDate(SelectedTransaction.Date)} " +
            $"на {MoneyText.Format(SelectedTransaction.AmountKopecks)}?");
        if (confirmed && _transactions.Delete(SelectedTransaction.Id))
        {
            SelectedTransaction = null;
            Reload();
        }
    }

    private void ShowCategories()
    {
        _dialogs.ShowCategories();

        // Имена и состав категорий могли измениться: обновляем и фильтр.
        long? selectedId = SelectedFilterCategory?.Id;
        RebuildFilterCategories();
        SetProperty(ref _selectedFilterCategory,
            FilterCategories.FirstOrDefault(c => c.Id == selectedId)
            ?? FilterCategories[0],
            nameof(SelectedFilterCategory));
        Reload();
    }

    /// <summary>Применяет произвольный период из полей «с» и «по».</summary>
    private void ApplyPeriod()
    {
        string fromText = FilterFromText.Trim();
        string toText = FilterToText.Trim();

        if (fromText.Length == 0 || toText.Length == 0)
        {
            FilterError = "Укажите обе даты периода.";
            return;
        }

        if (!TryParseDate(fromText, out DateOnly from) || !TryParseDate(toText, out DateOnly to))
        {
            FilterError = "Дата — в формате ДД.ММ.ГГГГ, например: 05.11.2026.";
            return;
        }

        if (from > to)
        {
            FilterError = "Начало периода позже его конца.";
            return;
        }

        _periodFrom = from;
        _periodTo = to;
        FilterError = string.Empty;
        IsCustomPeriod = true;
        Reload();
    }

    /// <summary>Возвращает журнал к режиму выбранного месяца.</summary>
    private void ResetPeriod()
    {
        FilterFromText = string.Empty;
        FilterToText = string.Empty;
        FilterError = string.Empty;
        IsCustomPeriod = false;
        Reload();
    }

    private void RebuildFilterCategories()
    {
        FilterCategories.Clear();
        FilterCategories.Add(new FilterCategoryOption(null, "Все категории"));
        foreach (Category category in _categories.ListAll())
        {
            FilterCategories.Add(new FilterCategoryOption(
                category.Id,
                $"{category.Name} ({(category.Kind == CategoryKind.Income ? "доход" : "расход")})"));
        }

        if (SelectedFilterCategory is null)
        {
            SetProperty(ref _selectedFilterCategory, FilterCategories[0], nameof(SelectedFilterCategory));
        }
    }

    private static bool TryParseDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "dd.MM.yyyy", Russian, DateTimeStyles.None, out date);

    private static string FormatDate(DateOnly date) => date.ToString("dd.MM.yyyy", Russian);

    private static string TitleOf(int year, int month)
    {
        string title = new DateTime(year, month, 1).ToString("MMMM yyyy", Russian);
        return char.ToUpper(title[0], CultureInfo.CurrentCulture) + title[1..];
    }

    /// <summary>Вариант фильтра категории; Id = null означает «все категории».</summary>
    public sealed record FilterCategoryOption(long? Id, string Display);
}
