using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Начислитель на реальной SQLite-базе: начисления за пропущенные даты,
/// отметка LastApplied, отсутствие дублей при повторном вызове,
/// неактивные шаблоны, новые шаблоны без задним числом.
/// </summary>
public class RecurringAccruerTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public RecurringAccruerTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private RecurringAccruer Accruer => new(_fixture.ContextFactory);

    private EfRecurringPaymentRepository Repository => new(_fixture.ContextFactory);

    [Fact]
    public void Accrual_CreatesTransactionWithTemplateFields()
    {
        // Новый шаблон (lastApplied null) начинает начисляться со
        // сегодняшнего дня: день 15, today — 15-е.
        long id = Repository.Add(3, 2, 25_000_00, 15, "аренда", isActive: true);

        int created = Accruer.AccrueUpTo(
            new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 15));

        Assert.Equal(1, created);
        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Transaction transaction = Assert.Single(context.Transactions.ToList());
        Assert.Equal(3, transaction.CategoryId);
        Assert.Equal(2, transaction.AccountId);
        Assert.Equal(25_000_00, transaction.AmountKopecks);
        Assert.Equal(new DateOnly(2026, 11, 15), transaction.Date);
        Assert.Equal("аренда", transaction.Comment);
        Assert.Equal(new DateOnly(2026, 11, 15), Repository.GetById(id)!.LastApplied);
    }

    [Fact]
    public void RepeatCall_DoesNotDuplicate()
    {
        Repository.Add(3, 1, 500_00, 15, null, isActive: true);
        var today = new DateOnly(2026, 11, 15);

        Assert.Equal(1, Accruer.AccrueUpTo(today, today));
        Assert.Equal(0, Accruer.AccrueUpTo(today, today));

        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Assert.Single(context.Transactions.ToList());
    }

    [Fact]
    public void MissedMonths_AreCaughtUp()
    {
        long id = Repository.Add(4, 1, 1_000_00, 5, null, isActive: true);
        // Начислили за сентябрь (lastApplied 05.09), приложение не
        // запускалось до ноября: октябрь и ноябрь догоняются.
        using (KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            RecurringPayment template = context.RecurringPayments.Single();
            template.LastApplied = new DateOnly(2026, 9, 5);
            context.SaveChanges();
        }

        int created = Accruer.AccrueUpTo(
            new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 15));

        Assert.Equal(2, created);
        using KopilkaDbContext check = _fixture.ContextFactory.CreateDbContext();
        Assert.Equal(
            new[] { new DateOnly(2026, 10, 5), new DateOnly(2026, 11, 5) },
            check.Transactions.OrderBy(t => t.Date).Select(t => t.Date).ToList());
        Assert.Equal(new DateOnly(2026, 11, 5), Repository.GetById(id)!.LastApplied);
    }

    [Fact]
    public void InactiveTemplate_IsSkipped()
    {
        Repository.Add(3, 1, 500_00, 10, null, isActive: false);

        int created = Accruer.AccrueUpTo(
            new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 15));

        Assert.Equal(0, created);
        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Assert.Empty(context.Transactions.ToList());
    }

    [Fact]
    public void NewTemplate_DoesNotAccrueRetroactively()
    {
        // Новый шаблон (lastApplied null) с днём 10: при вызове до
        // конца месяца начисляется только сегодняшний день и позже —
        // прошедшее 10-е не задним числом.
        Repository.Add(3, 1, 500_00, 10, null, isActive: true);

        int created = Accruer.AccrueUpTo(
            new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 30));

        Assert.Equal(0, created);
        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Assert.Empty(context.Transactions.ToList());
    }

    [Fact]
    public void OpenMonth_AccruesWholeMonth_IncludingFutureDays()
    {
        // Открытие декабря 15-го ноября разворачивает всё до конца
        // декабря: предстоящее 25-е ноября и декабрьские начисления
        // появляются сразу (задним числом — ничего: 5-е ноября уже
        // прошло, а новый шаблон назад не начисляется).
        Repository.Add(3, 1, 500_00, 5, null, isActive: true);
        Repository.Add(4, 2, 900_00, 25, null, isActive: true);

        int created = Accruer.AccrueUpTo(
            new DateOnly(2026, 11, 15), new DateOnly(2026, 12, 31));

        // День 5: ноябрьское 5-е до создания шаблона — не задним числом,
        // начисляется декабрьское; день 25: предстоящее 25-е ноября
        // и декабрьское 25-е.
        Assert.Equal(3, created);
        using KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext();
        Assert.Equal(
            new[] { new DateOnly(2026, 11, 25), new DateOnly(2026, 12, 5), new DateOnly(2026, 12, 25) },
            context.Transactions.OrderBy(t => t.Date).Select(t => t.Date).ToList());
    }
}
