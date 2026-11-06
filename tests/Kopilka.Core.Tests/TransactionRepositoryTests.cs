using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Kopilka.Core.Tests;

public class TransactionRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public TransactionRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private SqliteTransactionRepository Repository => new(_fixture.Database.Connection);

    // Сид V001: счета 1 «Наличные», 2 «Карта»; категории 1 «Зарплата» (доход),
    // 3 «Продукты» (расход), 4 «Транспорт» (расход). Идентификаторы фиксированы.

    [Fact]
    public void Add_ReturnsId_AndGetByIdReturnsSameTransaction()
    {
        var expected = new Transaction(0, 1, 3, 1_250, new DateOnly(2026, 1, 15), "хлеб и молоко");

        long id = Repository.Add(expected.AccountId, expected.CategoryId, expected.AmountKopecks, expected.Date, expected.Comment);

        Transaction? loaded = Repository.GetById(id);

        Assert.NotNull(loaded);
        Assert.Equal(id, loaded!.Id);
        Assert.Equal(expected.AccountId, loaded.AccountId);
        Assert.Equal(expected.CategoryId, loaded.CategoryId);
        Assert.Equal(expected.AmountKopecks, loaded.AmountKopecks);
        Assert.Equal(expected.Date, loaded.Date);
        Assert.Equal(expected.Comment, loaded.Comment);
    }

    [Fact]
    public void Add_NonPositiveAmount_IsRejectedBySchema()
    {
        Assert.Throws<SqliteException>(() =>
            Repository.Add(1, 3, 0, new DateOnly(2026, 1, 1), null));
        Assert.Throws<SqliteException>(() =>
            Repository.Add(1, 3, -100, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void ListByPeriod_IncludesBothBounds_AndSortsByDate()
    {
        var repository = Repository;
        long first = repository.Add(1, 3, 100, new DateOnly(2026, 3, 10), null);
        repository.Add(1, 3, 200, new DateOnly(2026, 3, 15), null);
        long last = repository.Add(1, 3, 300, new DateOnly(2026, 3, 20), null);

        IReadOnlyList<Transaction> inPeriod = repository.ListByPeriod(
            new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 20));

        Assert.Equal(3, inPeriod.Count);
        Assert.Equal(first, inPeriod[0].Id);
        Assert.Equal(last, inPeriod[2].Id);
        // Границы периода входят в выборку.
        Assert.Equal(new DateOnly(2026, 3, 10), inPeriod[0].Date);
        Assert.Equal(new DateOnly(2026, 3, 20), inPeriod[2].Date);
    }

    [Fact]
    public void ListByPeriod_ExcludesDatesOutsidePeriod()
    {
        var repository = Repository;
        repository.Add(1, 3, 100, new DateOnly(2026, 2, 28), null);
        repository.Add(1, 3, 100, new DateOnly(2026, 4, 1), null);

        IReadOnlyList<Transaction> marchOnly = repository.ListByPeriod(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Empty(marchOnly);
    }

    [Fact]
    public void ListByPeriod_WithCategory_FiltersByCategory()
    {
        var repository = Repository;
        repository.Add(1, 3, 100, new DateOnly(2026, 3, 5), null);
        repository.Add(1, 4, 100, new DateOnly(2026, 3, 6), null);
        repository.Add(1, 3, 100, new DateOnly(2026, 3, 7), null);

        IReadOnlyList<Transaction> products = repository.ListByPeriod(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), categoryId: 3);

        Assert.Equal(2, products.Count);
        Assert.All(products, t => Assert.Equal(3, t.CategoryId));
    }

    [Fact]
    public void ListByPeriod_ReturnsTransactionsFromAllAccounts()
    {
        var repository = Repository;
        repository.Add(1, 3, 100, new DateOnly(2026, 3, 1), null);
        repository.Add(2, 3, 100, new DateOnly(2026, 3, 2), null);

        IReadOnlyList<Transaction> all = repository.ListByPeriod(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Update_ChangesAmountCommentAndCategory()
    {
        var repository = Repository;
        long id = repository.Add(1, 3, 100, new DateOnly(2026, 3, 1), "было");

        bool updated = repository.Update(new Transaction(id, 2, 4, 999, new DateOnly(2026, 3, 1), "стало"));

        Assert.True(updated);
        Transaction? loaded = repository.GetById(id);
        Assert.Equal(2, loaded!.AccountId);
        Assert.Equal(4, loaded.CategoryId);
        Assert.Equal(999, loaded.AmountKopecks);
        Assert.Equal("стало", loaded.Comment);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        bool updated = Repository.Update(new Transaction(9999, 1, 3, 100, new DateOnly(2026, 3, 1), null));

        Assert.False(updated);
    }

    [Fact]
    public void Delete_RemovesTransaction()
    {
        var repository = Repository;
        long id = repository.Add(1, 3, 100, new DateOnly(2026, 3, 1), null);

        bool deleted = repository.Delete(id);

        Assert.True(deleted);
        Assert.Null(repository.GetById(id));
        Assert.False(repository.Delete(id));
    }

    [Fact]
    public void Comment_CanBeNull()
    {
        long id = Repository.Add(1, 3, 100, new DateOnly(2026, 3, 1), null);

        Transaction? loaded = Repository.GetById(id);

        Assert.Null(loaded!.Comment);
    }
}
