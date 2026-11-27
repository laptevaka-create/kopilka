using System.Collections.ObjectModel;
using System.Globalization;
using Kopilka.Core.Data;
using Kopilka.Core.Domain;

namespace Kopilka.App.Presentation;

/// <summary>
/// Окно шаблонов регулярных платежей: список по возрастанию дня,
/// добавление, правка, удаление, признак активности. Начисления
/// по активным шаблонам создаёт RecurringAccruer при запуске
/// и открытии месяца. Не знает о View — тестируется без интерфейса.
/// </summary>
public sealed class RecurringViewModel : ViewModelBase
{
    private readonly IRecurringPaymentRepository _payments;
    private readonly ICategoryRepository _categories;
    private readonly IAccountRepository _accounts;

    private RecurringItemViewModel? _selectedItem;
    private CategoryPick? _selectedCategory;
    private AccountPick? _selectedAccount;
    private string _amountText = string.Empty;
    private string _dayText = string.Empty;
    private string _commentText = string.Empty;
    private bool _isActive = true;
    private string _validationSummary = string.Empty;

    public RecurringViewModel(
        ICategoryRepository categories,
        IAccountRepository accounts,
        IRecurringPaymentRepository payments)
    {
        _categories = categories;
        _accounts = accounts;
        _payments = payments;

        Items = new ObservableCollection<RecurringItemViewModel>();
        CategoryOptions = categories.ListAll()
            .Select(c => new CategoryPick(c.Id, c.Name, c.Kind))
            .ToList();
        AccountOptions = accounts.ListAll()
            .Select(a => new AccountPick(a.Id, a.Name))
            .ToList();
        SelectedCategory = CategoryOptions.FirstOrDefault();
        SelectedAccount = AccountOptions.FirstOrDefault();

        AddTemplateCommand = new RelayCommand(AddTemplate);
        SaveChangesCommand = new RelayCommand(SaveChanges, () => SelectedItem is not null);
        DeleteTemplateCommand = new RelayCommand(DeleteTemplate, () => SelectedItem is not null);

        Reload();
    }

    /// <summary>Шаблоны по возрастанию дня месяца.</summary>
    public ObservableCollection<RecurringItemViewModel> Items { get; }

    /// <summary>Категории для выбора, с пометкой типа.</summary>
    public IReadOnlyList<CategoryPick> CategoryOptions { get; }

    /// <summary>Счета для выбора.</summary>
    public IReadOnlyList<AccountPick> AccountOptions { get; }

    /// <summary>Выбранный шаблон; заполняет панель редактирования.</summary>
    public RecurringItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                FillEditor(value);
                SaveChangesCommand.RaiseCanExecuteChanged();
                DeleteTemplateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public CategoryPick? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public AccountPick? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    /// <summary>Сумма в рублях, как её вводит пользователь.</summary>
    public string AmountText
    {
        get => _amountText;
        set => SetProperty(ref _amountText, value);
    }

    /// <summary>День месяца 1–31, как введён.</summary>
    public string DayText
    {
        get => _dayText;
        set => SetProperty(ref _dayText, value);
    }

    public string CommentText
    {
        get => _commentText;
        set => SetProperty(ref _commentText, value);
    }

    /// <summary>Шаблон активен: по нему начисления создаются.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    /// <summary>Ошибки последней операции, построчно.</summary>
    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    public RelayCommand AddTemplateCommand { get; }

    public RelayCommand SaveChangesCommand { get; }

    public RelayCommand DeleteTemplateCommand { get; }

    /// <summary>Перечитывает список шаблонов.</summary>
    public void Reload()
    {
        Items.Clear();
        foreach (RecurringPayment template in _payments.ListAll())
        {
            string categoryName = CategoryOptions.FirstOrDefault(c => c.Id == template.CategoryId)?.Name ?? "(удалена)";
            string accountName = AccountOptions.FirstOrDefault(a => a.Id == template.AccountId)?.Name ?? "(удалён)";
            Items.Add(new RecurringItemViewModel(
                template.Id, categoryName, accountName,
                template.AmountKopecks, template.DayOfMonth,
                template.Comment, template.IsActive));
        }
    }

    private void FillEditor(RecurringItemViewModel? item)
    {
        if (item is null)
        {
            SelectedCategory = CategoryOptions.FirstOrDefault();
            SelectedAccount = AccountOptions.FirstOrDefault();
            AmountText = string.Empty;
            DayText = string.Empty;
            CommentText = string.Empty;
            IsActive = true;
        }
        else
        {
            RecurringPayment? template = _payments.GetById(item.Id);
            SelectedCategory = CategoryOptions.FirstOrDefault(c => c.Id == template!.CategoryId);
            SelectedAccount = AccountOptions.FirstOrDefault(a => a.Id == template!.AccountId);
            AmountText = MoneyText.ForInput(template!.AmountKopecks);
            DayText = template.DayOfMonth.ToString(CultureInfo.InvariantCulture);
            CommentText = template.Comment ?? string.Empty;
            IsActive = template.IsActive;
        }

        ValidationSummary = string.Empty;
    }

    private void AddTemplate()
    {
        if (!TryReadInput(out long kopecks, out int day, out string? comment))
        {
            return;
        }

        _payments.Add(SelectedCategory!.Id, SelectedAccount!.Id, kopecks, day, comment, IsActive);
        ValidationSummary = string.Empty;
        Reload();
    }

    private void SaveChanges()
    {
        if (SelectedItem is null)
        {
            return;
        }

        if (!TryReadInput(out long kopecks, out int day, out string? comment))
        {
            return;
        }

        bool updated = _payments.Update(new RecurringPayment
        {
            Id = SelectedItem.Id,
            CategoryId = SelectedCategory!.Id,
            AccountId = SelectedAccount!.Id,
            AmountKopecks = kopecks,
            DayOfMonth = day,
            Comment = comment,
            IsActive = IsActive,
        });
        if (!updated)
        {
            ValidationSummary = "Шаблон уже удалён.";
            Reload();
            return;
        }

        ValidationSummary = string.Empty;
        long selectedId = SelectedItem.Id;
        Reload();
        SelectedItem = Items.FirstOrDefault(i => i.Id == selectedId);
    }

    private void DeleteTemplate()
    {
        if (SelectedItem is null)
        {
            return;
        }

        if (!_payments.Delete(SelectedItem.Id))
        {
            ValidationSummary = "Шаблон уже удалён.";
            Reload();
            return;
        }

        ValidationSummary = string.Empty;
        Reload();
        SelectedItem = null;
    }

    /// <summary>Проверяет ввод панели; при ошибке заполняет сводку и возвращает false.</summary>
    private bool TryReadInput(out long kopecks, out int day, out string? comment)
    {
        var errors = new List<string>();

        if (SelectedCategory is null)
        {
            errors.Add("Выберите категорию.");
        }

        if (SelectedAccount is null)
        {
            errors.Add("Выберите счёт.");
        }

        if (!int.TryParse(DayText.Trim(), out day) || day is < 1 or > 31)
        {
            errors.Add("День месяца — число от 1 до 31.");
        }

        if (!MoneyText.TryParseRubles(AmountText, out kopecks, out bool tooBig))
        {
            errors.Add(tooBig ? "Сумма слишком большая." : "Сумма — число, например: 1250,50.");
        }
        else if (kopecks <= 0)
        {
            errors.Add("Сумма должна быть больше нуля.");
        }

        comment = string.IsNullOrWhiteSpace(CommentText) ? null : CommentText.Trim();

        if (errors.Count > 0)
        {
            ValidationSummary = string.Join(Environment.NewLine, errors);
            return false;
        }

        ValidationSummary = string.Empty;
        return true;
    }

    /// <summary>Строка списка шаблонов.</summary>
    public sealed record RecurringItemViewModel(
        long Id,
        string CategoryName,
        string AccountName,
        long AmountKopecks,
        int DayOfMonth,
        string? Comment,
        bool IsActive)
    {
        /// <summary>Подпись: «Продукты — 5 000,00 ₽, 5-е число, Карта».</summary>
        public string Display =>
            $"{CategoryName} — {MoneyText.Format(AmountKopecks)}, {DayOfMonth}-е число, {AccountName}" +
            (IsActive ? string.Empty : " — выкл");
    }
}
