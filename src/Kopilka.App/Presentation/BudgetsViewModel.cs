using System.Collections.ObjectModel;
using Kopilka.Core.Data;
using Kopilka.Core.Domain;

namespace Kopilka.App.Presentation;

/// <summary>
/// Модель представления окна бюджетов: категории расходов с признаком
/// лимита и их лимиты на конкретный месяц. Лимит задаётся в рублях,
/// пара (категория, месяц) уникальна — повторное сохранение обновляет.
/// Не знает о View — тестируется без интерфейса.
/// </summary>
public sealed class BudgetsViewModel : ViewModelBase
{
    private readonly ICategoryRepository _categories;
    private readonly IBudgetRepository _budgets;
    private readonly string _month;

    private BudgetItemViewModel? _selectedItem;
    private string _limitText = string.Empty;
    private string _validationSummary = string.Empty;

    public BudgetsViewModel(
        ICategoryRepository categories,
        IBudgetRepository budgets,
        string month)
    {
        _categories = categories;
        _budgets = budgets;
        _month = month;

        Items = new ObservableCollection<BudgetItemViewModel>();
        SaveLimitCommand = new RelayCommand(SaveLimit, () => SelectedItem is not null);
        RemoveLimitCommand = new RelayCommand(RemoveLimit, () => SelectedItem is not null);

        Reload();
    }

    /// <summary>Заголовок месяца для окна: «Ноябрь 2026».</summary>
    public string MonthTitle { get; private set; } = string.Empty;

    /// <summary>Категории с лимитом и их бюджеты на месяц.</summary>
    public ObservableCollection<BudgetItemViewModel> Items { get; }

    /// <summary>Выбранная строка; заполняет поле лимита.</summary>
    public BudgetItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                LimitText = value?.LimitKopecks is long kopecks
                    ? MoneyText.ForInput(kopecks)
                    : string.Empty;
                SaveLimitCommand.RaiseCanExecuteChanged();
                RemoveLimitCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Лимит выбранной категории в рублях, как введён.</summary>
    public string LimitText
    {
        get => _limitText;
        set => SetProperty(ref _limitText, value);
    }

    /// <summary>Ошибки последней операции.</summary>
    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    public RelayCommand SaveLimitCommand { get; }

    public RelayCommand RemoveLimitCommand { get; }

    /// <summary>Перечитывает категории с лимитом и бюджеты месяца.</summary>
    public void Reload()
    {
        int year = int.Parse(_month[..4]);
        int month = int.Parse(_month[5..7]);
        MonthTitle = CalendarText.MonthTitle(year, month);

        Dictionary<long, long> budgetsByCategory = _budgets.ListByMonth(_month)
            .ToDictionary(b => b.CategoryId, b => b.LimitKopecks);

        Items.Clear();
        foreach (Category category in _categories.ListAll())
        {
            if (category.Kind != CategoryKind.Expense || !category.HasLimit)
            {
                continue;
            }

            Items.Add(new BudgetItemViewModel(
                category.Id,
                category.Name,
                budgetsByCategory.TryGetValue(category.Id, out long limit) ? limit : null));
        }
    }

    private void SaveLimit()
    {
        if (SelectedItem is null)
        {
            return;
        }

        string text = LimitText.Trim();
        if (text.Length == 0)
        {
            ValidationSummary = "Введите лимит.";
            return;
        }

        if (!MoneyText.TryParseRubles(text, out long kopecks, out bool tooBig))
        {
            ValidationSummary = tooBig
                ? "Лимит слишком большой."
                : "Лимит — число, например: 15000.";
            return;
        }

        if (kopecks <= 0)
        {
            ValidationSummary = "Лимит должен быть больше нуля.";
            return;
        }

        _budgets.Set(SelectedItem.CategoryId, _month, kopecks);
        ValidationSummary = string.Empty;

        long selectedId = SelectedItem.CategoryId;
        Reload();
        SelectedItem = Items.FirstOrDefault(i => i.CategoryId == selectedId);
    }

    private void RemoveLimit()
    {
        if (SelectedItem is null)
        {
            return;
        }

        if (!_budgets.Remove(SelectedItem.CategoryId, _month))
        {
            ValidationSummary = "Бюджет на этот месяц не задан.";
            Reload();
            return;
        }

        ValidationSummary = string.Empty;
        LimitText = string.Empty;

        long selectedId = SelectedItem.CategoryId;
        Reload();
        SelectedItem = Items.FirstOrDefault(i => i.CategoryId == selectedId);
    }

    /// <summary>Строка окна бюджетов: категория и её лимит на месяц (или нет).</summary>
    public sealed record BudgetItemViewModel(long CategoryId, string CategoryName, long? LimitKopecks)
    {
        /// <summary>Подпись в списке: «Продукты — 15 000,00 ₽» или «Продукты — не задан».</summary>
        public string Display =>
            LimitKopecks is long kopecks
                ? $"{CategoryName} — {MoneyText.Format(kopecks)}"
                : $"{CategoryName} — не задан";
    }
}
