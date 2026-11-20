using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kopilka.Core.Tests;

public class AccountRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public AccountRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private EfAccountRepository Repository => new(_fixture.ContextFactory);

    [Fact]
    public void ListAll_ReturnsTwoPresetAccounts()
    {
        IReadOnlyList<Account> accounts = Repository.ListAll();

        Assert.Equal(2, accounts.Count);
        Assert.Equal("Наличные", accounts[0].Name);
        Assert.Equal("Карта", accounts[1].Name);
    }

    [Fact]
    public void Add_CreatesAccount_AndGetByIdReturnsIt()
    {
        var repository = Repository;
        long id = repository.Add("Депозит", sortOrder: 5);

        Account? loaded = repository.GetById(id);

        Assert.NotNull(loaded);
        Assert.Equal("Депозит", loaded!.Name);
        Assert.Equal(5, loaded.SortOrder);
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        Account? account = Repository.GetById(9999);

        Assert.Null(account);
    }

    [Fact]
    public void Update_RenamesAccount()
    {
        var repository = Repository;
        long id = repository.Add("Старое имя", sortOrder: 0);

        bool updated = repository.Update(new Account { Id = id, Name = "Новое имя", SortOrder = 0 });

        Assert.True(updated);
        Assert.Equal("Новое имя", repository.GetById(id)!.Name);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        bool updated = Repository.Update(new Account { Id = 9999, Name = "Имя", SortOrder = 0 });

        Assert.False(updated);
    }

    [Fact]
    public void Delete_UnusedAccount_ReturnsDeleted()
    {
        var repository = Repository;
        long id = repository.Add("Временный счёт", sortOrder: 0);

        DeleteResult result = repository.Delete(id);

        Assert.Equal(DeleteResult.Deleted, result);
        Assert.Null(repository.GetById(id));
    }

    [Fact]
    public void Delete_AccountWithTransactions_ReturnsInUse()
    {
        var repository = Repository;
        long id = repository.Add("Счёт с движением", sortOrder: 0);
        var transactions = new EfTransactionRepository(_fixture.ContextFactory);
        transactions.Add(id, categoryId: 1, amountKopecks: 100, date: new DateOnly(2026, 1, 1), comment: null);

        DeleteResult result = repository.Delete(id);

        Assert.Equal(DeleteResult.InUse, result);
        Assert.NotNull(repository.GetById(id));
    }

    [Fact]
    public void Delete_AccountWithRecurringTemplate_ReturnsInUse()
    {
        // Шаблоны регулярных платежей ссылаются на счёт (шаг 6):
        // счёт под шаблоном не удаляется — отказ с причиной.
        var repository = Repository;
        long id = repository.Add("Счёт под подписку", sortOrder: 0);
        var template = new RecurringPayment
        {
            CategoryId = 3,
            AccountId = id,
            AmountKopecks = 500,
            DayOfMonth = 5,
            Comment = null,
            IsActive = true,
        };
        using (Kopilka.Core.Data.KopilkaDbContext context = _fixture.ContextFactory.CreateDbContext())
        {
            context.RecurringPayments.Add(template);
            context.SaveChanges();
        }

        try
        {
            DeleteResult result = repository.Delete(id);

            Assert.Equal(DeleteResult.InUse, result);
        }
        finally
        {
            using Kopilka.Core.Data.KopilkaDbContext cleanup = _fixture.ContextFactory.CreateDbContext();
            RecurringPayment? created = cleanup.RecurringPayments.Find(template.Id);
            if (created is not null)
            {
                cleanup.RecurringPayments.Remove(created);
                cleanup.SaveChanges();
            }
        }
    }

    [Fact]
    public void Delete_UnknownId_ReturnsNotFound()
    {
        DeleteResult result = Repository.Delete(9999);

        Assert.Equal(DeleteResult.NotFound, result);
    }

    [Fact]
    public void Add_DuplicateName_IsRejectedByUniqueIndex()
    {
        Assert.Throws<DbUpdateException>(() => Repository.Add("Наличные", sortOrder: 0));
    }
}
