using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kopilka.Core.Data;

/// <summary>Справочник счетов поверх EF Core.</summary>
public sealed class EfAccountRepository : IAccountRepository
{
    private readonly IDbContextFactory<KopilkaDbContext> _contextFactory;

    public EfAccountRepository(IDbContextFactory<KopilkaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<Account> ListAll()
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        return context.Accounts
            .AsNoTracking()
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToList();
    }

    public Account? GetById(long id)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        return context.Accounts.AsNoTracking().FirstOrDefault(a => a.Id == id);
    }

    public long Add(string name, int sortOrder)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        var account = new Account { Name = name, SortOrder = sortOrder };
        context.Accounts.Add(account);
        context.SaveChanges();

        return account.Id;
    }

    public bool Update(Account account)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        Account? existing = context.Accounts.Find(account.Id);
        if (existing is null)
        {
            return false;
        }

        existing.Name = account.Name;
        existing.SortOrder = account.SortOrder;
        context.SaveChanges();

        return true;
    }

    public DeleteResult Delete(long id)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        // Использование — все ссылки на счёт: транзакции и шаблоны
        // регулярных платежей. Иначе репозиторий дошёл бы до SaveChanges
        // и упал необработанным исключением.
        if (context.Transactions.Any(t => t.AccountId == id)
            || context.RecurringPayments.Any(r => r.AccountId == id))
        {
            return DeleteResult.InUse;
        }

        Account? existing = context.Accounts.Find(id);
        if (existing is null)
        {
            return DeleteResult.NotFound;
        }

        context.Accounts.Remove(existing);
        context.SaveChanges();

        return DeleteResult.Deleted;
    }
}
