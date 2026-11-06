using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контрактные тесты журнала транзакций: границы периода (первый и
/// последний день включительно), соседние месяцы и граница года,
/// фильтр по категории без совпадений, порядок при нескольких
/// транзакциях в один день, спецсимволы в комментарии и смена
/// комментария null ↔ текст через Update.
/// </summary>
public class TransactionRepositoryContractTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public TransactionRepositoryContractTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetToSeed();
    }

    private SqliteTransactionRepository Repository => new(_fixture.Database.Connection);

    // Сид V001: счёт 1 «Наличные», 2 «Карта»; категории 3 «Продукты»,
    // 4 «Транспорт», 5 «Развлечения» (расходы). Идентификаторы фиксированы.

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        Transaction? loaded = Repository.GetById(9999);

        Assert.Null(loaded);
    }

    [Fact]
    public void ListByPeriod_MonthBounds_FirstAndLastDayIncluded_NeighborMonthsExcluded()
    {
        var repository = Repository;
        // Порядок вставки намеренно не совпадает с порядком дат:
        // проверяется сортировка по дате, а не по id.
        long lastDay = repository.Add(1, 3, 100, new DateOnly(2026, 2, 28), null);
        repository.Add(1, 3, 100, new DateOnly(2026, 1, 31), null); // соседний месяц слева
        repository.Add(1, 3, 100, new DateOnly(2026, 3, 1), null); // соседний месяц справа
        long firstDay = repository.Add(1, 3, 100, new DateOnly(2026, 2, 1), null);

        IReadOnlyList<Transaction> february = repository.ListByPeriod(
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        Assert.Equal(2, february.Count);
        Assert.Equal(firstDay, february[0].Id);
        Assert.Equal(lastDay, february[1].Id);
    }

    [Fact]
    public void ListByPeriod_YearBoundary_DecemberAndJanuary_BothIncludedAndOrdered()
    {
        var repository = Repository;
        long januarySecond = repository.Add(1, 3, 100, new DateOnly(2026, 1, 2), null);
        long december30 = repository.Add(1, 3, 100, new DateOnly(2025, 12, 30), null);
        long december31 = repository.Add(1, 3, 100, new DateOnly(2025, 12, 31), null);
        long januaryFirst = repository.Add(1, 3, 100, new DateOnly(2026, 1, 1), null);

        IReadOnlyList<Transaction> spanning = repository.ListByPeriod(
            new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(4, spanning.Count);
        // Текстовое сравнение 'YYYY-MM-DD' корректно через границу года:
        // декабрь 2025 стоит раньше января 2026 независимо от порядка id.
        Assert.Equal(december30, spanning[0].Id);
        Assert.Equal(december31, spanning[1].Id);
        Assert.Equal(januaryFirst, spanning[2].Id);
        Assert.Equal(januarySecond, spanning[3].Id);
    }

    [Fact]
    public void ListByPeriod_YearBoundary_DecemberPeriod_ExcludesNextJanuary()
    {
        var repository = Repository;
        repository.Add(1, 3, 100, new DateOnly(2026, 1, 1), null);
        long december31 = repository.Add(1, 3, 100, new DateOnly(2025, 12, 31), null);

        IReadOnlyList<Transaction> december = repository.ListByPeriod(
            new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 31));

        Transaction onlyDecember = Assert.Single(december);
        Assert.Equal(december31, onlyDecember.Id);
    }

    [Fact]
    public void ListByPeriod_CategoryFilterWithoutMatches_ReturnsEmpty()
    {
        var repository = Repository;
        repository.Add(1, 3, 100, new DateOnly(2026, 3, 10), null); // «Продукты», март
        repository.Add(1, 4, 100, new DateOnly(2026, 4, 10), null); // «Транспорт», апрель

        IReadOnlyList<Transaction> transportInMarch = repository.ListByPeriod(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), categoryId: 4);
        IReadOnlyList<Transaction> entertainmentInYear = repository.ListByPeriod(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), categoryId: 5);

        // Транзакции категории есть, но вне периода; и категория без транзакций вовсе.
        Assert.Empty(transportInMarch);
        Assert.Empty(entertainmentInYear);
    }

    [Fact]
    public void ListByPeriod_SeveralTransactionsSameDay_OrdersByDateThenId()
    {
        var repository = Repository;
        long laterDate = repository.Add(1, 3, 100, new DateOnly(2026, 5, 2), null);
        long first = repository.Add(1, 3, 100, new DateOnly(2026, 5, 1), null);
        long second = repository.Add(2, 4, 200, new DateOnly(2026, 5, 1), null);
        long third = repository.Add(1, 5, 300, new DateOnly(2026, 5, 1), null);

        IReadOnlyList<Transaction> may = repository.ListByPeriod(
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 2));

        Assert.Equal(4, may.Count);
        // Внутри одного дня — порядок по id; дата важнее id:
        // транзакция 2 мая добавлена первой, но стоит последней.
        Assert.Equal(first, may[0].Id);
        Assert.Equal(second, may[1].Id);
        Assert.Equal(third, may[2].Id);
        Assert.Equal(laterDate, may[3].Id);
    }

    [Fact]
    public void Comment_CyrillicQuotesSemicolonsAndLineBreaks_RoundTrips()
    {
        string comment = "«Хлеб» и \"молоко\"; всё — со скидкой; ёлка\tи\r\nперенос строки";
        var repository = Repository;
        long id = repository.Add(1, 3, 1_000, new DateOnly(2026, 3, 5), comment);

        Transaction? loaded = repository.GetById(id);

        Assert.Equal(comment, loaded!.Comment);
    }

    [Fact]
    public void Comment_LooksLikeSql_IsStoredAsPlainText()
    {
        string comment = "'); DELETE FROM transactions; --";
        var repository = Repository;
        long id = repository.Add(1, 3, 1_000, new DateOnly(2026, 3, 5), comment);

        Transaction? loaded = repository.GetById(id);
        IReadOnlyList<Transaction> wholeYear = repository.ListByPeriod(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        // Параметры экранируются: «SQL» в комментарии — обычный текст.
        Assert.Equal(comment, loaded!.Comment);
        Assert.Single(wholeYear);
    }

    [Fact]
    public void Comment_TenThousandCharacters_RoundTrips()
    {
        string comment = new string('ё', 9_995) + "«»;\";";
        var repository = Repository;
        long id = repository.Add(1, 3, 1_000, new DateOnly(2026, 3, 5), comment);

        Transaction? loaded = repository.GetById(id);

        Assert.Equal(10_000, loaded!.Comment!.Length);
        Assert.Equal(comment, loaded.Comment);
    }

    [Fact]
    public void Comment_EmptyString_StaysEmptyText_NotNull()
    {
        var repository = Repository;
        long id = repository.Add(1, 3, 100, new DateOnly(2026, 3, 5), "");

        Transaction? loaded = repository.GetById(id);

        Assert.NotNull(loaded!.Comment);
        Assert.Equal(string.Empty, loaded.Comment);
    }

    [Fact]
    public void Update_Comment_FromNullToText_SavesComment()
    {
        var repository = Repository;
        long id = repository.Add(1, 3, 100, new DateOnly(2026, 3, 5), null);
        Assert.Null(repository.GetById(id)!.Comment);

        bool updated = repository.Update(new Transaction(
            id, 1, 3, 100, new DateOnly(2026, 3, 5), "теперь с комментарием"));

        Assert.True(updated);
        Assert.Equal("теперь с комментарием", repository.GetById(id)!.Comment);
    }

    [Fact]
    public void Update_Comment_FromTextToNull_ClearsComment()
    {
        var repository = Repository;
        long id = repository.Add(1, 3, 100, new DateOnly(2026, 3, 5), "был комментарий");

        bool updated = repository.Update(new Transaction(
            id, 1, 3, 100, new DateOnly(2026, 3, 5), null));

        Assert.True(updated);
        Assert.Null(repository.GetById(id)!.Comment);
    }

    [Fact]
    public void Update_Comment_WithQuotesAndSemicolons_RoundTrips()
    {
        var repository = Repository;
        long id = repository.Add(1, 3, 100, new DateOnly(2026, 3, 5), "старый");
        string special = "новый: «с кавычками»; и \"точкой с запятой\"; --";

        bool updated = repository.Update(new Transaction(
            id, 1, 3, 100, new DateOnly(2026, 3, 5), special));

        Assert.True(updated);
        Assert.Equal(special, repository.GetById(id)!.Comment);
    }
}
