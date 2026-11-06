using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контрактные тесты справочника счетов: ограничение UNIQUE на имя
/// (дубликат предустановленного и собственного имени, переименование
/// в занятое имя), спецсимволы в имени и порядок выборки при равном
/// sort_order.
/// </summary>
public class AccountRepositoryContractTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public AccountRepositoryContractTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private SqliteAccountRepository Repository => new(_fixture.Database.Connection);

    // Сид V001: счёт 1 «Наличные» (sort_order 0), 2 «Карта» (sort_order 1).

    [Fact]
    public void Add_DuplicateOfPresetName_IsRejectedByUniqueConstraint()
    {
        Assert.Throws<SqliteException>(() => Repository.Add("Наличные", sortOrder: 0));

        // Отказ не оставил следов: справочник не изменился.
        Assert.Equal(2, Repository.ListAll().Count);
    }

    [Fact]
    public void Add_DuplicateOfOwnName_IsRejectedByUniqueConstraint()
    {
        var repository = Repository;
        repository.Add("Депозит", sortOrder: 0);

        Assert.Throws<SqliteException>(() => repository.Add("Депозит", sortOrder: 1));
    }

    [Fact]
    public void Update_RenameToOccupiedName_IsRejectedByUniqueConstraint()
    {
        var repository = Repository;
        long first = repository.Add("Первый", sortOrder: 0);
        long second = repository.Add("Второй", sortOrder: 0);

        Assert.Throws<SqliteException>(() => repository.Update(new Account(second, "Первый", 0)));

        // Имя второго счёта не изменилось, первый счёт на месте.
        Assert.Equal("Второй", repository.GetById(second)!.Name);
        Assert.NotNull(repository.GetById(first));
    }

    [Fact]
    public void Add_NameWithQuotesAndSemicolon_RoundTrips()
    {
        string name = "Счёт «зарплатный»; \"основной\"";
        var repository = Repository;
        long id = repository.Add(name, sortOrder: 3);

        Assert.Equal(name, repository.GetById(id)!.Name);
    }

    [Fact]
    public void ListAll_EqualSortOrders_AreOrderedByName()
    {
        var repository = Repository;
        repository.Add("Альфа", sortOrder: 0);
        repository.Add("Бета", sortOrder: 0);

        IReadOnlyList<Account> accounts = repository.ListAll();

        // Группа sort_order 0 упорядочена по имени: «Альфа», «Бета»,
        // «Наличные»; затем «Карта» с sort_order 1.
        Assert.Equal(
            new[] { "Альфа", "Бета", "Наличные", "Карта" },
            accounts.Select(a => a.Name).ToArray());
    }
}
