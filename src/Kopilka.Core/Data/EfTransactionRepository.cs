using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kopilka.Core.Data;

/// <summary>Журнал транзакций поверх EF Core.</summary>
public sealed class EfTransactionRepository : ITransactionRepository
{
    private readonly IDbContextFactory<KopilkaDbContext> _contextFactory;

    public EfTransactionRepository(IDbContextFactory<KopilkaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public long Add(long accountId, long categoryId, long amountKopecks, DateOnly date, string? comment)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        var transaction = new Transaction
        {
            AccountId = accountId,
            CategoryId = categoryId,
            AmountKopecks = amountKopecks,
            Date = date,
            Comment = comment,
        };
        context.Transactions.Add(transaction);
        context.SaveChanges();

        return transaction.Id;
    }

    public Transaction? GetById(long id)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        return context.Transactions.AsNoTracking().FirstOrDefault(t => t.Id == id);
    }

    public IReadOnlyList<Transaction> ListByPeriod(DateOnly from, DateOnly to, long? categoryId = null)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        IQueryable<Transaction> query = context.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= from && t.Date <= to);

        if (categoryId is not null)
        {
            query = query.Where(t => t.CategoryId == categoryId);
        }

        return query
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();
    }

    public IReadOnlyDictionary<long, long> SumByCategory(DateOnly from, DateOnly to)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        return context.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= from && t.Date <= to)
            .GroupBy(t => t.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.AmountKopecks));
    }

    public bool Update(Transaction transaction)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        Transaction? existing = context.Transactions.Find(transaction.Id);
        if (existing is null)
        {
            return false;
        }

        existing.AccountId = transaction.AccountId;
        existing.CategoryId = transaction.CategoryId;
        existing.AmountKopecks = transaction.AmountKopecks;
        existing.Date = transaction.Date;
        existing.Comment = transaction.Comment;
        context.SaveChanges();

        return true;
    }

    public bool Delete(long id)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        Transaction? existing = context.Transactions.Find(id);
        if (existing is null)
        {
            return false;
        }

        context.Transactions.Remove(existing);
        context.SaveChanges();

        return true;
    }
}
