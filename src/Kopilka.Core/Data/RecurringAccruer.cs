using Kopilka.Core.Domain;
using Kopilka.Core.Engines;
using Microsoft.EntityFrameworkCore;

namespace Kopilka.Core.Data;

/// <summary>
/// Начислитель регулярных платежей. Один контекст на вызов: все
/// транзакции и отметки LastApplied сохраняются одной транзакцией
/// базы — сбой посреди начисления не оставляет ни дублей, ни
/// наполовину проставленных отметок.
/// </summary>
public sealed class RecurringAccruer : IRecurringAccruer
{
    private readonly IDbContextFactory<KopilkaDbContext> _contextFactory;

    public RecurringAccruer(IDbContextFactory<KopilkaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public int AccrueUpTo(DateOnly today, DateOnly toDate)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        List<RecurringPayment> templates = context.RecurringPayments
            .Where(r => r.IsActive)
            .ToList();

        int created = 0;
        foreach (RecurringPayment template in templates)
        {
            // Пропущенные даты — после последнего начисления; новый
            // шаблон начисляется начиная с сегодняшнего дня.
            DateOnly from = template.LastApplied is DateOnly applied
                ? applied.AddDays(1)
                : today;

            IReadOnlyList<DateOnly> dates = RecurringEngine.GenerateDates(
                template.DayOfMonth, template.LastApplied, from, toDate);

            foreach (DateOnly date in dates)
            {
                context.Transactions.Add(new Transaction
                {
                    AccountId = template.AccountId,
                    CategoryId = template.CategoryId,
                    AmountKopecks = template.AmountKopecks,
                    Date = date,
                    Comment = template.Comment,
                });
                created++;
            }

            if (dates.Count > 0)
            {
                template.LastApplied = dates[^1];
            }
        }

        if (created > 0)
        {
            context.SaveChanges();
        }

        return created;
    }
}
