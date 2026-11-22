using System.Globalization;
using Kopilka.Core.Data;
using Kopilka.Core.Time;

namespace Kopilka.App.Presentation;

/// <summary>
/// Модель представления диалога добавления транзакции. Валидирует ввод
/// и пишет транзакцию в репозиторий. Все ошибки показываются списком
/// с причинами — отказ объясняется, а не молчит.
/// </summary>
public sealed class TransactionEditViewModel : ViewModelBase
{
    private readonly ITransactionRepository _transactions;
    private readonly IClock _clock;

    private AccountOption? _selectedAccount;
    private CategoryOption? _selectedCategory;
    private DateTime? _selectedDate;
    private string _amountText = string.Empty;
    private string _commentText = string.Empty;
    private string _validationSummary = string.Empty;
    private string _title = "Добавить транзакцию";
    private long? _editingId;

    public TransactionEditViewModel(
        IAccountRepository accounts,
        ICategoryRepository categories,
        ITransactionRepository transactions,
        IClock clock)
    {
        _transactions = transactions;
        _clock = clock;

        Accounts = accounts.ListAll()
            .Select(a => new AccountOption(a.Id, a.Name))
            .ToList();
        Categories = categories.ListAll()
            .Select(c => new CategoryOption(c.Id, c.Name, c.Kind))
            .ToList();

        SelectedAccount = Accounts.FirstOrDefault();
        SelectedDate = clock.Now.Date;
    }

    /// <summary>Заголовок окна: зависит от режима (добавление/редактирование).</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>true, если диалог редактирует существующую транзакцию.</summary>
    public bool IsEditing => _editingId is not null;

    /// <summary>Счета для выбора.</summary>
    public IReadOnlyList<AccountOption> Accounts { get; }

    /// <summary>Категории для выбора, с пометкой типа.</summary>
    public IReadOnlyList<CategoryOption> Categories { get; }

    public AccountOption? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    public CategoryOption? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    /// <summary>Дата транзакции; время отрезается при сохранении.</summary>
    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value);
    }

    /// <summary>Сумма в рублях, как её вводит пользователь.</summary>
    public string AmountText
    {
        get => _amountText;
        set => SetProperty(ref _amountText, value);
    }

    public string CommentText
    {
        get => _commentText;
        set => SetProperty(ref _commentText, value);
    }

    /// <summary>Ошибки последней попытки сохранения, построчно.</summary>
    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    /// <summary>
    /// Переводит диалог в режим редактирования: поля заполняются
    /// значениями существующей транзакции, Save обновит её, а не
    /// создаст новую.
    /// </summary>
    public void BeginEdit(Kopilka.Core.Domain.Transaction transaction)
    {
        _editingId = transaction.Id;
        Title = "Изменить транзакцию";

        SelectedAccount = Accounts.FirstOrDefault(a => a.Id == transaction.AccountId);
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == transaction.CategoryId);
        SelectedDate = transaction.Date.ToDateTime(TimeOnly.MinValue);
        // В поле ввода — сумма без символа валюты, как её вводит пользователь.
        AmountText = (transaction.AmountKopecks / 100m).ToString("0.00", new CultureInfo("ru-RU"));
        CommentText = transaction.Comment ?? string.Empty;
        ValidationSummary = string.Empty;
    }

    /// <summary>
    /// Проверяет ввод и сохраняет транзакцию: в режиме добавления создаёт
    /// новую, в режиме редактирования обновляет существующую. Возвращает
    /// true при успехе; при ошибках заполняет ValidationSummary и
    /// возвращает false.
    /// </summary>
    public bool Save()
    {
        List<string> errors = Validate();
        if (errors.Count > 0)
        {
            ValidationSummary = string.Join(Environment.NewLine, errors);
            return false;
        }

        string? comment = string.IsNullOrWhiteSpace(CommentText) ? null : CommentText.Trim();
        DateOnly date = DateOnly.FromDateTime(SelectedDate!.Value.Date);

        if (_editingId is long id)
        {
            bool updated = _transactions.Update(new Kopilka.Core.Domain.Transaction
            {
                Id = id,
                AccountId = SelectedAccount!.Id,
                CategoryId = SelectedCategory!.Id,
                AmountKopecks = ParseAmount(AmountText),
                Date = date,
                Comment = comment,
            });
            if (!updated)
            {
                // Транзакцию удалили, пока диалог был открыт.
                ValidationSummary = "Транзакция уже удалена.";
                return false;
            }
        }
        else
        {
            _transactions.Add(
                SelectedAccount!.Id,
                SelectedCategory!.Id,
                ParseAmount(AmountText),
                date,
                comment);
        }

        // Успешная попытка закрывает предыдущие ошибки: сводка всегда
        // описывает последнюю попытку, а не всю историю ввода.
        ValidationSummary = string.Empty;

        return true;
    }

    private List<string> Validate()
    {
        var errors = new List<string>();

        if (SelectedAccount is null)
        {
            errors.Add("Выберите счёт.");
        }

        if (SelectedCategory is null)
        {
            errors.Add("Выберите категорию.");
        }

        if (SelectedDate is null)
        {
            errors.Add("Выберите дату.");
        }

        string amount = AmountText.Trim();
        if (amount.Length == 0)
        {
            errors.Add("Введите сумму.");
        }
        else if (!TryParseAmount(amount, out long kopecks, out bool tooBig))
        {
            if (tooBig)
            {
                errors.Add("Сумма слишком большая.");
            }
            else
            {
                errors.Add("Сумма — число, например: 1250,50.");
            }
        }
        else if (kopecks <= 0)
        {
            errors.Add("Сумма должна быть больше нуля.");
        }

        return errors;
    }

    /// <summary>Рубли с запятой или точкой → копейки; точка считается опечаткой запятой.</summary>
    private static long ParseAmount(string text)
    {
        TryParseAmount(text, out long kopecks, out _);
        return kopecks;
    }

    private static bool TryParseAmount(string text, out long kopecks, out bool tooBig)
    {
        tooBig = false;
        string normalized = text.Trim().Replace('.', ',');
        if (!decimal.TryParse(normalized, NumberStyles.Number, new CultureInfo("ru-RU"), out decimal rubles))
        {
            kopecks = 0;
            return false;
        }

        decimal rounded = Math.Round(rubles * 100m, MidpointRounding.AwayFromZero);
        if (rounded > long.MaxValue || rounded < long.MinValue)
        {
            // Столько копеек не помещается в long: транзакция с такой суммой
            // невозможна, отказываем с причиной, а не падаем при приведении типа.
            kopecks = 0;
            tooBig = true;
            return false;
        }

        kopecks = (long)rounded;
        return true;
    }

    /// <summary>Вариант счёта для выпадающего списка.</summary>
    public sealed record AccountOption(long Id, string Name);

    /// <summary>Вариант категории для выпадающего списка, с типом.</summary>
    public sealed record CategoryOption(long Id, string Name, Kopilka.Core.Domain.CategoryKind Kind)
    {
        /// <summary>Подпись в списке: «Продукты (расход)».</summary>
        public string Display => $"{Name} ({(Kind == Kopilka.Core.Domain.CategoryKind.Income ? "доход" : "расход")})";
    }
}
