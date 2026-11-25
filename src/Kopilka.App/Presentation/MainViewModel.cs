using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Kopilka.Core.Engines;
using Kopilka.Core.Time;
using Microsoft.EntityFrameworkCore;

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
    private readonly IBudgetRepository _budgets;
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
        IBudgetRepository budgets,
        IDialogService dialogs,
        IClock clock)
    {
        _transactions = transactions;
        _accounts = accounts;
        _categories = categories;
        _budgets = budgets;
        _dialogs = dialogs;
        _clock = clock;

        Transactions = new ObservableCollection<TransactionRowViewModel>();
        FilterCategories = new ObservableCollection<FilterCategoryOption>();
        Budgets = new ObservableCollection<BudgetRowViewModel>();

        PreviousMonthCommand = new RelayCommand(GoToPreviousMonth, () => !IsCustomPeriod);
        NextMonthCommand = new RelayCommand(GoToNextMonth, () => !IsCustomPeriod);
        AddTransactionCommand = new RelayCommand(AddTransaction);
        EditTransactionCommand = new RelayCommand(EditTransaction, () => SelectedTransaction is not null);
        DeleteTransactionCommand = new RelayCommand(DeleteTransaction, () => SelectedTransaction is not null);
        ShowCategoriesCommand = new RelayCommand(ShowCategories);
        ShowBudgetsCommand = new RelayCommand(ShowBudgets, () => !IsCustomPeriod);
        ShowReportCommand = new RelayCommand(ShowMonthReport, () => !IsCustomPeriod);
        ExportCsvCommand = new RelayCommand(ExportCsv, () => !IsCustomPeriod);
        ImportCsvCommand = new RelayCommand(ImportCsv);
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

    /// <summary>
    /// Панель бюджетов выбранного месяца: категории с лимитом, лимит,
    /// потрачено, остаток, «доступно в день», статус.
    /// </summary>
    public ObservableCollection<BudgetRowViewModel> Budgets { get; }

    /// <summary>
    /// Панель бюджетов видна только в режиме месяца: бюджет задан на
    /// месяц, и к произвольному периоду он не привязан.
    /// </summary>
    public bool BudgetsVisible => !IsCustomPeriod;

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
                ShowBudgetsCommand.RaiseCanExecuteChanged();
                ShowReportCommand.RaiseCanExecuteChanged();
                ExportCsvCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(BudgetsVisible));
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

    public RelayCommand ShowBudgetsCommand { get; }

    public RelayCommand ShowReportCommand { get; }

    public RelayCommand ExportCsvCommand { get; }

    public RelayCommand ImportCsvCommand { get; }

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
            ? $"Период: {CalendarText.FormatDate(_periodFrom)} — {CalendarText.FormatDate(_periodTo)}"
            : CalendarText.MonthTitle(_year, _month);

        ReloadBudgets(from, to);
    }

    /// <summary>
    /// Перечитывает панель бюджетов выбранного месяца. Потрачено
    /// считается по всем транзакциям месяца, без фильтра журнала:
    /// фильтр журнала — про просмотр, бюджет — про контроль.
    /// </summary>
    private void ReloadBudgets(DateOnly from, DateOnly to)
    {
        Budgets.Clear();
        if (IsCustomPeriod)
        {
            return;
        }

        int daysInMonth = DateTime.DaysInMonth(_year, _month);
        int currentDay = CurrentDayForShownMonth(daysInMonth);

        Dictionary<long, long> budgetsByCategory = _budgets.ListByMonth(MonthKey())
            .ToDictionary(b => b.CategoryId, b => b.LimitKopecks);
        IReadOnlyDictionary<long, long> spentByCategory = _transactions.SumByCategory(from, to);

        foreach (Category category in _categories.ListAll())
        {
            if (category.Kind != CategoryKind.Expense || !category.HasLimit)
            {
                continue;
            }

            bool hasBudget = budgetsByCategory.TryGetValue(category.Id, out long limit);
            spentByCategory.TryGetValue(category.Id, out long spent);

            BudgetSnapshot? snapshot = hasBudget
                ? BudgetEngine.Compute(limit, spent, currentDay, daysInMonth)
                : null;

            Budgets.Add(new BudgetRowViewModel(
                category.Id, category.Name, hasBudget,
                hasBudget ? limit : 0, spent, snapshot));
        }
    }

    /// <summary>
    /// Ключ месяца для таблицы бюджетов: «2026-11».
    /// </summary>
    private string MonthKey() => $"{_year:D4}-{_month:D2}";

    /// <summary>
    /// Текущий день для формулы «доступно в день»: у текущего месяца —
    /// сегодня; у прошлого — последний день (месяц весь прошёл); у
    /// будущего — первый (месяц весь впереди).
    /// </summary>
    private int CurrentDayForShownMonth(int daysInMonth)
    {
        int shown = _year * 12 + _month;
        int now = _clock.Now.Year * 12 + _clock.Now.Month;

        if (shown == now)
        {
            return _clock.Now.Day;
        }

        return shown < now ? daysInMonth : 1;
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
            $"Удалить транзакцию от {CalendarText.FormatDate(SelectedTransaction.Date)} " +
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

    private void ShowBudgets()
    {
        // Окно редактирует бюджеты показанного месяца; после закрытия
        // панель перечитывается.
        _dialogs.ShowBudgets(MonthKey());
        Reload();
    }

    private void ShowMonthReport()
    {
        _dialogs.ShowMonthReport(MonthKey());
    }

    /// <summary>Экспорт транзакций показанного месяца в CSV-файл.</summary>
    private void ExportCsv()
    {
        string? path = _dialogs.AskCsvExportPath(MonthKey());
        if (path is null)
        {
            return; // отмена диалога — не ошибка
        }

        DateOnly from = new(_year, _month, 1);
        DateOnly to = new(_year, _month, DateTime.DaysInMonth(_year, _month));

        Dictionary<long, string> accountNames = _accounts.ListAll()
            .ToDictionary(a => a.Id, a => a.Name);
        Dictionary<long, string> categoryNames = _categories.ListAll()
            .ToDictionary(c => c.Id, c => c.Name);

        string csv = CsvExchange.BuildExportText(
            _transactions.ListByPeriod(from, to), accountNames, categoryNames);

        try
        {
            // BOM в UTF-8: Excel открывает кириллицу корректно;
            // чтение метку снимает.
            File.WriteAllText(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Экспорт CSV", $"Не удалось записать файл:\n{path}\n\n{ex.Message}");
        }
    }

    /// <summary>
    /// Импорт транзакций из CSV: построчная проверка, валидные строки
    /// сохраняются, битые пропускаются с причиной; итог — отчёт.
    /// </summary>
    private void ImportCsv()
    {
        string? path = _dialogs.AskCsvImportPath();
        if (path is null)
        {
            return; // отмена диалога — не ошибка
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Импорт CSV", $"Не удалось прочитать файл:\n{path}\n\n{ex.Message}");
            return;
        }

        Dictionary<string, long> accountsByName = _accounts.ListAll()
            .ToDictionary(a => a.Name, a => a.Id);
        Dictionary<string, IReadOnlyList<long>> categoriesByName = _categories.ListAll()
            .GroupBy(c => c.Name)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<long>)g.Select(c => c.Id).ToList());

        CsvValidationResult result = CsvValidator.Validate(text, accountsByName, categoriesByName);

        var skipped = new List<CsvSkippedLine>(result.Skipped);
        int imported = 0;
        foreach (ParsedCsvLine line in result.Valid)
        {
            try
            {
                _transactions.Add(line.AccountId, line.CategoryId, line.AmountKopecks, line.Date, line.Comment);
                imported++;
            }
            catch (DbUpdateException)
            {
                // Строка прошла проверку, но база отказала — попадает
                // в пропущенные с причиной, импорт продолжается.
                skipped.Add(new CsvSkippedLine(line.LineNumber, string.Empty, "не сохранена: база отказала"));
            }
        }

        _dialogs.ShowCsvImportReport(CsvExchange.BuildImportReport(imported, skipped));
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

    /// <summary>Вариант фильтра категории; Id = null означает «все категории».</summary>
    public sealed record FilterCategoryOption(long? Id, string Display);
}
