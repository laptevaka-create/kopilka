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
        new Account(1, "Наличные", 0),
        new Account(2, "Карта", 1),
    };

    private long _nextId = 100;

    public IReadOnlyList<Account> ListAll() => _accounts;

    public Account? GetById(long id) => _accounts.FirstOrDefault(a => a.Id == id);

    public long Add(string name, int sortOrder)
    {
        var account = new Account(_nextId++, name, sortOrder);
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
        new Category(1, "Зарплата", CategoryKind.Income, HasLimit: false, SortOrder: 0),
        new Category(2, "Прочие доходы", CategoryKind.Income, HasLimit: false, SortOrder: 1),
        new Category(3, "Продукты", CategoryKind.Expense, HasLimit: false, SortOrder: 0),
        new Category(4, "Транспорт", CategoryKind.Expense, HasLimit: false, SortOrder: 1),
        new Category(5, "Развлечения", CategoryKind.Expense, HasLimit: false, SortOrder: 2),
        new Category(6, "Прочие расходы", CategoryKind.Expense, HasLimit: false, SortOrder: 3),
    };

    private long _nextId = 100;

    public IReadOnlyList<Category> ListAll() => _categories;

    public Category? GetById(long id) => _categories.FirstOrDefault(c => c.Id == id);

    public long Add(string name, CategoryKind kind, bool hasLimit, int sortOrder)
    {
        var category = new Category(_nextId++, name, kind, hasLimit, sortOrder);
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
        var transaction = new Transaction(_nextId++, accountId, categoryId, amountKopecks, date, comment);
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

internal sealed class FakeDialogService(bool result) : IDialogService
{
    /// <summary>Сколько раз диалог был показан.</summary>
    public int ShownCount { get; private set; }

    public bool ShowAddTransaction()
    {
        ShownCount++;
        return result;
    }
}
