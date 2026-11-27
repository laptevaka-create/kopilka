using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контракт начислителя на реальной SQLite-базе: цепочки вызовов,
/// перенос null-комментария, несколько шаблонов, выключение и повторное
/// включение, неприкосновенность истории и атомарность SaveChanges.
/// Идентификаторы шаблонов берутся только из возвращаемого Add id.
/// </summary>
public class RecurringAccruerContractTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public RecurringAccruerContractTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private RecurringAccruer Accruer => new(_fixture.ContextFactory);

    private EfRecurringPaymentRepository Repository => new(_fixture.ContextFactory);

    [Fact]
    public void AccrueUpTo_SecondCallThroughDecember_CreatesDecemberWithoutDuplicatingNovember()
    {
        long id = Repository.Add(3, 1, 25_000_00, 25, null, isActive: true);
        var today = new DateOnly(2026, 11, 15);

        Assert.Equal(1, Accruer.AccrueUpTo(today, new DateOnly(2026, 11, 30)));
        Assert.Equal(1, Accruer.AccrueUpTo(today, new DateOnly(2026, 12, 31)));

        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Assert.Equal(
            new[] { new DateOnly(2026, 11, 25), new DateOnly(2026, 12, 25) },
            context.Transactions.OrderBy(t => t.Date).Select(t => t.Date).ToArray());
        Assert.Equal(new DateOnly(2026, 12, 25), Repository.GetById(id)!.LastApplied);
    }

    [Fact]
    public void AccrueUpTo_NullTemplateComment_CreatesTransactionWithNullComment()
    {
        Repository.Add(3, 2, 500_00, 15, comment: null, isActive: true);
        var today = new DateOnly(2026, 11, 15);

        Assert.Equal(1, Accruer.AccrueUpTo(today, today));

        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Transaction transaction = Assert.Single(context.Transactions.ToList());
        Assert.Null(transaction.Comment);
    }

    [Fact]
    public void AccrueUpTo_TwoTemplatesSameDate_CreatesBothInOneCall()
    {
        long productsId = Repository.Add(3, 1, 500_00, 15, null, isActive: true);
        long transportId = Repository.Add(4, 2, 900_00, 15, "проезд", isActive: true);
        var today = new DateOnly(2026, 11, 15);

        Assert.Equal(2, Accruer.AccrueUpTo(today, today));

        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Transaction[] transactions = context.Transactions.OrderBy(t => t.CategoryId).ToArray();
        Assert.Equal(2, transactions.Length);
        Assert.Equal(3, transactions[0].CategoryId);
        Assert.Equal(500_00, transactions[0].AmountKopecks);
        Assert.Equal(4, transactions[1].CategoryId);
        Assert.Equal(900_00, transactions[1].AmountKopecks);
        Assert.All(transactions, t => Assert.Equal(today, t.Date));
        Assert.Equal(today, Repository.GetById(productsId)!.LastApplied);
        Assert.Equal(today, Repository.GetById(transportId)!.LastApplied);
    }

    [Fact]
    public void AccrueUpTo_DeactivatedThenReactivated_SkipsWhileOffAndCatchesUpAfterOn()
    {
        long id = Repository.Add(3, 1, 1_000_00, 5, null, isActive: true);
        SetLastApplied(id, new DateOnly(2026, 9, 5));
        var today = new DateOnly(2026, 11, 15);

        // Октябрь и ноябрь начислены; декабрь пока не начислялся.
        Assert.Equal(2, Accruer.AccrueUpTo(today, new DateOnly(2026, 11, 30)));

        RecurringPayment disabled = Repository.GetById(id)!;
        disabled.IsActive = false;
        Assert.True(Repository.Update(disabled));
        Assert.Equal(0, Accruer.AccrueUpTo(today, new DateOnly(2026, 12, 31)));
        Assert.Equal(new DateOnly(2026, 11, 5), Repository.GetById(id)!.LastApplied);

        // Update не трогает LastApplied: после включения старая отметка
        // позволяет догнать пропущенный декабрь без дублей октября/ноября.
        RecurringPayment enabled = Repository.GetById(id)!;
        enabled.IsActive = true;
        Assert.True(Repository.Update(enabled));
        Assert.Equal(1, Accruer.AccrueUpTo(today, new DateOnly(2026, 12, 31)));

        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Assert.Equal(
            new[] { new DateOnly(2026, 10, 5), new DateOnly(2026, 11, 5), new DateOnly(2026, 12, 5) },
            context.Transactions.OrderBy(t => t.Date).Select(t => t.Date).ToArray());
        Assert.Equal(new DateOnly(2026, 12, 5), Repository.GetById(id)!.LastApplied);
    }

    [Fact]
    public void Delete_AccruedTemplate_LeavesCreatedTransactionsUntouched()
    {
        long templateId = Repository.Add(3, 2, 750_00, 15, "связь", isActive: true);
        var today = new DateOnly(2026, 11, 15);
        Assert.Equal(1, Accruer.AccrueUpTo(today, today));

        long transactionId;
        using (KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            transactionId = Assert.Single(context.Transactions.ToList()).Id;
        }

        Assert.True(Repository.Delete(templateId));
        Assert.Null(Repository.GetById(templateId));

        using KopilkaDbContext check = _fixture.ContextFactory.CreateDbContext();
        Transaction transaction = Assert.Single(check.Transactions.ToList());
        Assert.Equal(transactionId, transaction.Id);
        Assert.Equal(3, transaction.CategoryId);
        Assert.Equal(2, transaction.AccountId);
        Assert.Equal(750_00, transaction.AmountKopecks);
        Assert.Equal(today, transaction.Date);
        Assert.Equal("связь", transaction.Comment);
        Assert.Equal(0, Accruer.AccrueUpTo(today, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void AccrueUpTo_SaveChangesFails_RollsBackTransactionsAndLastApplied()
    {
        long goodId = Repository.Add(3, 1, 500_00, 15, null, isActive: true);
        long brokenId = Repository.Add(4, 2, 900_00, 15, null, isActive: true);

        // Моделируем сбой на единственном SaveChanges: у второго шаблона
        // категория больше не существует. PRAGMA действует только на это
        // соединение и сразу возвращается в ON; начислитель работает с FK.
        using (KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
            context.Database.ExecuteSql(
                $"UPDATE recurring SET category_id = 999 WHERE id = {brokenId}");
            context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON");
        }

        var today = new DateOnly(2026, 11, 15);
        Assert.Throws<DbUpdateException>(() => Accruer.AccrueUpTo(today, today));

        using (KopilkaDbContext check = _fixture.ContextFactory.CreateDbContext())
        {
            Assert.Empty(check.Transactions.ToList());
            Assert.Null(check.RecurringPayments.Single(r => r.Id == goodId).LastApplied);
            Assert.Null(check.RecurringPayments.Single(r => r.Id == brokenId).LastApplied);
        }

        // После устранения причины повтор создаёт ровно по одной записи:
        // неудачный вызов не оставил ни транзакций, ни наполовину отметок.
        RecurringPayment broken = Repository.GetById(brokenId)!;
        broken.CategoryId = 4;
        Assert.True(Repository.Update(broken));
        Assert.Equal(2, Accruer.AccrueUpTo(today, today));

        using KopilkaDbContext after = _fixture.ContextFactory.CreateDbContext();
        Assert.Equal(2, after.Transactions.Count());
        Assert.Equal(today, after.RecurringPayments.Single(r => r.Id == goodId).LastApplied);
        Assert.Equal(today, after.RecurringPayments.Single(r => r.Id == brokenId).LastApplied);
    }

    private void SetLastApplied(long id, DateOnly date)
    {
        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        RecurringPayment template = context.RecurringPayments.Single(r => r.Id == id);
        template.LastApplied = date;
        context.SaveChanges();
    }
}
