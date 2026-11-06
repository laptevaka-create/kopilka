using Kopilka.Core.Data;
using Kopilka.Core.Domain;
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

    private SqliteAccountRepository Repository => new(_fixture.Database.Connection);

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

        bool updated = repository.Update(new Account(id, "Новое имя", 0));

        Assert.True(updated);
        Assert.Equal("Новое имя", repository.GetById(id)!.Name);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        bool updated = Repository.Update(new Account(9999, "Имя", 0));

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
        var transactions = new SqliteTransactionRepository(_fixture.Database.Connection);
        transactions.Add(id, categoryId: 1, amountKopecks: 100, date: new DateOnly(2026, 1, 1), comment: null);

        DeleteResult result = repository.Delete(id);

        Assert.Equal(DeleteResult.InUse, result);
        Assert.NotNull(repository.GetById(id));
    }

    [Fact]
    public void Delete_UnknownId_ReturnsNotFound()
    {
        DeleteResult result = Repository.Delete(9999);

        Assert.Equal(DeleteResult.NotFound, result);
    }
}
