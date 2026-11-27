using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kopilka.Core.Data;

/// <summary>Шаблоны регулярных платежей поверх EF Core.</summary>
public sealed class EfRecurringPaymentRepository : IRecurringPaymentRepository
{
    private readonly IDbContextFactory<KopilkaDbContext> _contextFactory;

    public EfRecurringPaymentRepository(IDbContextFactory<KopilkaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<RecurringPayment> ListAll()
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        return context.RecurringPayments
            .AsNoTracking()
            .OrderBy(r => r.DayOfMonth)
            .ThenBy(r => r.Id)
            .ToList();
    }

    public RecurringPayment? GetById(long id)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        return context.RecurringPayments.AsNoTracking().FirstOrDefault(r => r.Id == id);
    }

    public long Add(long categoryId, long accountId, long amountKopecks, int dayOfMonth, string? comment, bool isActive)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        var template = new RecurringPayment
        {
            CategoryId = categoryId,
            AccountId = accountId,
            AmountKopecks = amountKopecks,
            DayOfMonth = dayOfMonth,
            Comment = comment,
            IsActive = isActive,
        };
        context.RecurringPayments.Add(template);
        context.SaveChanges();

        return template.Id;
    }

    public bool Update(RecurringPayment template)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        RecurringPayment? existing = context.RecurringPayments.Find(template.Id);
        if (existing is null)
        {
            return false;
        }

        existing.CategoryId = template.CategoryId;
        existing.AccountId = template.AccountId;
        existing.AmountKopecks = template.AmountKopecks;
        existing.DayOfMonth = template.DayOfMonth;
        existing.Comment = template.Comment;
        existing.IsActive = template.IsActive;
        context.SaveChanges();

        return true;
    }

    public bool Delete(long id)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        RecurringPayment? existing = context.RecurringPayments.Find(id);
        if (existing is null)
        {
            return false;
        }

        context.RecurringPayments.Remove(existing);
        context.SaveChanges();

        return true;
    }
}
