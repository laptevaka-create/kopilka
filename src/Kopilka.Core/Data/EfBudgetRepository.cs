using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kopilka.Core.Data;

/// <summary>Месячные бюджеты поверх EF Core.</summary>
public sealed class EfBudgetRepository : IBudgetRepository
{
    private readonly IDbContextFactory<KopilkaDbContext> _contextFactory;

    public EfBudgetRepository(IDbContextFactory<KopilkaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<Budget> ListByMonth(string month)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        return context.Budgets
            .AsNoTracking()
            .Where(b => b.Month == month)
            .OrderBy(b => b.CategoryId)
            .ToList();
    }

    public Budget? GetByCategoryAndMonth(long categoryId, string month)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        return context.Budgets
            .AsNoTracking()
            .FirstOrDefault(b => b.CategoryId == categoryId && b.Month == month);
    }

    public void Set(long categoryId, string month, long limitKopecks)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        Budget? existing = context.Budgets
            .FirstOrDefault(b => b.CategoryId == categoryId && b.Month == month);
        if (existing is null)
        {
            context.Budgets.Add(new Budget
            {
                CategoryId = categoryId,
                Month = month,
                LimitKopecks = limitKopecks,
            });
        }
        else
        {
            existing.LimitKopecks = limitKopecks;
        }

        context.SaveChanges();
    }

    public bool Remove(long categoryId, string month)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        Budget? existing = context.Budgets
            .FirstOrDefault(b => b.CategoryId == categoryId && b.Month == month);
        if (existing is null)
        {
            return false;
        }

        context.Budgets.Remove(existing);
        context.SaveChanges();

        return true;
    }
}
