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
    /// Проверяет ввод и сохраняет транзакцию. Возвращает true при успехе;
    /// при ошибках заполняет ValidationSummary и возвращает false.
    /// </summary>
    public bool Save()
    {
        List<string> errors = Validate();
        if (errors.Count > 0)
        {
            ValidationSummary = string.Join(Environment.NewLine, errors);
            return false;
        }

        _transactions.Add(
            SelectedAccount!.Id,
            SelectedCategory!.Id,
            ParseAmount(AmountText),
            DateOnly.FromDateTime(SelectedDate!.Value.Date),
            string.IsNullOrWhiteSpace(CommentText) ? null : CommentText.Trim());

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
