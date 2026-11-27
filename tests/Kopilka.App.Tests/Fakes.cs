using Kopilka.App.Presentation;
using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Kopilka.Core.Time;

namespace Kopilka.App.Tests;

/// <summary>
/// Подмены зависимостей для тестов моделей представления: часы
/// с фиксированной датой, репозитории в памяти и фейковый сервис
/// диалогов. Никакой базы и интерфейса — проверяется только логика.
/// </summary>
internal static class Fakes
{
    /// <summary>Часы, стоящие на 15 ноября 2026 года.</summary>
    public static IClock Clock() => new FixedClock(new DateTime(2026, 11, 15, 12, 0, 0));

    public static IClock Clock(DateTime moment) => new FixedClock(moment);

    public static FakeAccountRepository Accounts() => new();

    public static FakeCategoryRepository Categories() => new();

    public static FakeTransactionRepository Transactions() => new();

    public static FakeBudgetRepository Budgets() => new();

    public static FakeRecurringAccruer Accruer() => new();

    /// <summary>Сервис диалогов, всегда отвечающий заданным результатом.</summary>
    public static FakeDialogService Dialog(bool result) => new(result);
}

internal sealed class FixedClock(DateTime moment) : IClock
{
    public DateTime Now => moment;
    public DateTime UtcNow => moment.ToUniversalTime();
}

internal sealed class FakeAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts = new()
    {
        new Account { Id = 1, Name = "Наличные", SortOrder = 0 },
        new Account { Id = 2, Name = "Карта", SortOrder = 1 },
    };

    private long _nextId = 100;

    public IReadOnlyList<Account> ListAll() => _accounts;

    public Account? GetById(long id) => _accounts.FirstOrDefault(a => a.Id == id);

    public long Add(string name, int sortOrder)
    {
        var account = new Account { Id = _nextId++, Name = name, SortOrder = sortOrder };
        _accounts.Add(account);
        return account.Id;
    }

    public bool Update(Account account)
    {
        int index = _accounts.FindIndex(a => a.Id == account.Id);
        if (index < 0)
        {
            return false;
        }

        _accounts[index] = account;
        return true;
    }

    public DeleteResult Delete(long id)
    {
        Account? account = GetById(id);
        if (account is null)
        {
            return DeleteResult.NotFound;
        }

        _accounts.Remove(account);
        return DeleteResult.Deleted;
    }
}

internal sealed class FakeCategoryRepository : ICategoryRepository
{
    private readonly List<Category> _categories = new()
    {
        new Category { Id = 1, Name = "Зарплата", Kind = CategoryKind.Income, HasLimit = false, SortOrder = 0 },
        new Category { Id = 2, Name = "Прочие доходы", Kind = CategoryKind.Income, HasLimit = false, SortOrder = 1 },
        new Category { Id = 3, Name = "Продукты", Kind = CategoryKind.Expense, HasLimit = true, SortOrder = 0 },
        new Category { Id = 4, Name = "Транспорт", Kind = CategoryKind.Expense, HasLimit = false, SortOrder = 1 },
        new Category { Id = 5, Name = "Развлечения", Kind = CategoryKind.Expense, HasLimit = false, SortOrder = 2 },
        new Category { Id = 6, Name = "Прочие расходы", Kind = CategoryKind.Expense, HasLimit = false, SortOrder = 3 },
    };

    private long _nextId = 100;

    /// <summary>Идентификаторы категорий, которые «используются»: Delete вернёт InUse.</summary>
    public HashSet<long> InUseIds { get; } = new();

    public IReadOnlyList<Category> ListAll() =>
        // Контракт репозитория: сначала расходы, затем доходы;
        // внутри типа — по sort_order, затем по имени.
        _categories
            .OrderBy(c => c.Kind == CategoryKind.Income)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToList();

    public Category? GetById(long id) => _categories.FirstOrDefault(c => c.Id == id);

    public long Add(string name, CategoryKind kind, bool hasLimit, int sortOrder)
    {
        var category = new Category { Id = _nextId++, Name = name, Kind = kind, HasLimit = hasLimit, SortOrder = sortOrder };
        _categories.Add(category);
        return category.Id;
    }

    public bool Update(Category category)
    {
        int index = _categories.FindIndex(c => c.Id == category.Id);
        if (index < 0)
        {
            return false;
        }

        _categories[index] = category;
        return true;
    }

    public DeleteResult Delete(long id)
    {
        Category? category = GetById(id);
        if (category is null)
        {
            return DeleteResult.NotFound;
        }

        if (InUseIds.Contains(id))
        {
            return DeleteResult.InUse;
        }

        _categories.Remove(category);
        return DeleteResult.Deleted;
    }
}

internal sealed class FakeTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions = new();
    private long _nextId = 1;

    public IReadOnlyList<Transaction> All => _transactions;

    public long Add(long accountId, long categoryId, long amountKopecks, DateOnly date, string? comment)
    {
        var transaction = new Transaction
        {
            Id = _nextId++,
            AccountId = accountId,
            CategoryId = categoryId,
            AmountKopecks = amountKopecks,
            Date = date,
            Comment = comment,
        };
        _transactions.Add(transaction);
        return transaction.Id;
    }

    public Transaction? GetById(long id) => _transactions.FirstOrDefault(t => t.Id == id);

    public IReadOnlyList<Transaction> ListByPeriod(DateOnly from, DateOnly to, long? categoryId = null) =>
        _transactions
            .Where(t => t.Date >= from && t.Date <= to)
            .Where(t => categoryId is null || t.CategoryId == categoryId)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();

    public IReadOnlyDictionary<long, long> SumByCategory(DateOnly from, DateOnly to) =>
        _transactions
            .Where(t => t.Date >= from && t.Date <= to)
            .GroupBy(t => t.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.AmountKopecks));

    public bool Update(Transaction transaction)
    {
        int index = _transactions.FindIndex(t => t.Id == transaction.Id);
        if (index < 0)
        {
            return false;
        }

        _transactions[index] = transaction;
        return true;
    }

    public bool Delete(long id)
    {
        Transaction? transaction = GetById(id);
        if (transaction is null)
        {
            return false;
        }

        _transactions.Remove(transaction);
        return true;
    }
}

internal sealed class FakeBudgetRepository : IBudgetRepository
{
    private readonly List<Budget> _budgets = new();
    private long _nextId = 1;

    public IReadOnlyList<Budget> ListByMonth(string month) =>
        _budgets
            .Where(b => b.Month == month)
            .OrderBy(b => b.CategoryId)
            .ToList();

    public Budget? GetByCategoryAndMonth(long categoryId, string month) =>
        _budgets.FirstOrDefault(b => b.CategoryId == categoryId && b.Month == month);

    public void Set(long categoryId, string month, long limitKopecks)
    {
        Budget? existing = _budgets.FirstOrDefault(b => b.CategoryId == categoryId && b.Month == month);
        if (existing is null)
        {
            _budgets.Add(new Budget
            {
                Id = _nextId++,
                CategoryId = categoryId,
                Month = month,
                LimitKopecks = limitKopecks,
            });
        }
        else
        {
            existing.LimitKopecks = limitKopecks;
        }
    }

    public bool Remove(long categoryId, string month)
    {
        Budget? existing = _budgets.FirstOrDefault(b => b.CategoryId == categoryId && b.Month == month);
        if (existing is null)
        {
            return false;
        }

        _budgets.Remove(existing);
        return true;
    }
}

internal sealed class FakeDialogService : IDialogService
{
    private readonly bool _addResult;

    public FakeDialogService(bool result)
    {
        _addResult = result;
        EditResult = result;
        ConfirmResult = result;
    }
    /// <summary>Сколько раз диалог добавления был показан.</summary>
    public int ShownCount { get; private set; }

    /// <summary>Сколько раз диалог редактирования был показан.</summary>
    public int EditShownCount { get; private set; }

    /// <summary>Сколько раз открывалось окно категорий.</summary>
    public int CategoriesShownCount { get; private set; }

    /// <summary>Сколько раз открывалось окно бюджетов.</summary>
    public int BudgetsShownCount { get; private set; }

    /// <summary>Месяц, с которым последний раз открывали окно бюджетов.</summary>
    public string? LastBudgetsMonth { get; private set; }

    /// <summary>Сколько раз открывалось окно отчёта месяца.</summary>
    public int ReportShownCount { get; private set; }

    /// <summary>Месяц, с которым последний раз открывали отчёт.</summary>
    public string? LastReportMonth { get; private set; }

    /// <summary>
    /// Путь, который «выбирает» пользователь для экспорта;
    /// null — отмена диалога сохранения.
    /// </summary>
    public string? ExportPath { get; set; }

    /// <summary>
    /// Путь, который «выбирает» пользователь для импорта;
    /// null — отмена диалога открытия.
    /// </summary>
    public string? ImportPath { get; set; }

    /// <summary>Сколько раз показывался отчёт импорта CSV.</summary>
    public int ImportReportShownCount { get; private set; }

    /// <summary>Текст последнего отчёта импорта CSV.</summary>
    public string? LastImportReport { get; private set; }

    /// <summary>Сколько раз показывалось сообщение об ошибке.</summary>
    public int ErrorShownCount { get; private set; }

    /// <summary>Заголовок последней ошибки.</summary>
    public string? LastErrorTitle { get; private set; }

    /// <summary>Текст последней ошибки.</summary>
    public string? LastErrorMessage { get; private set; }

    /// <summary>Идентификатор последней транзакции, переданной в редактирование.</summary>
    public long? LastEditId { get; private set; }

    /// <summary>Результат диалога редактирования; по умолчанию — общий result.</summary>
    public bool EditResult { get; set; }

    /// <summary>Результат подтверждения; по умолчанию — общий result.</summary>
    public bool ConfirmResult { get; set; }

    /// <summary>Последний текст подтверждения.</summary>
    public string? LastConfirmMessage { get; private set; }

    public bool ShowAddTransaction()
    {
        ShownCount++;
        return _addResult;
    }

    public bool ShowEditTransaction(long transactionId)
    {
        EditShownCount++;
        LastEditId = transactionId;
        return EditResult;
    }

    public void ShowCategories()
    {
        CategoriesShownCount++;
    }

    public void ShowBudgets(string month)
    {
        BudgetsShownCount++;
        LastBudgetsMonth = month;
    }

    /// <summary>Сколько раз открывалось окно регулярных платежей.</summary>
    public int RecurringShownCount { get; private set; }

    public void ShowRecurring()
    {
        RecurringShownCount++;
    }

    public void ShowMonthReport(string month)
    {
        ReportShownCount++;
        LastReportMonth = month;
    }

    public string? AskCsvExportPath(string monthKey) => ExportPath;

    public string? AskCsvImportPath() => ImportPath;

    public void ShowCsvImportReport(string report)
    {
        ImportReportShownCount++;
        LastImportReport = report;
    }

    public void ShowError(string title, string message)
    {
        ErrorShownCount++;
        LastErrorTitle = title;
        LastErrorMessage = message;
    }

    public bool Confirm(string title, string message)
    {
        LastConfirmMessage = message;
        return ConfirmResult;
    }
}

/// <summary>
/// Фейк начислителя: ничего не создаёт, но фиксирует вызовы —
/// «до какой даты» и «сколько раз».
/// </summary>
internal sealed class FakeRecurringAccruer : IRecurringAccruer
{
    /// <summary>Сколько раз вызывалось начисление.</summary>
    public int Calls { get; private set; }

    /// <summary>Нижняя граница последнего вызова (сегодня для новых шаблонов).</summary>
    public DateOnly? LastToday { get; private set; }

    /// <summary>Верхняя граница последнего вызова (до какой даты начислять).</summary>
    public DateOnly? LastToDate { get; private set; }

    /// <summary>Число, которое начисление «создало».</summary>
    public int Result { get; set; }

    public int AccrueUpTo(DateOnly today, DateOnly toDate)
    {
        Calls++;
        LastToday = today;
        LastToDate = toDate;
        return Result;
    }
}
