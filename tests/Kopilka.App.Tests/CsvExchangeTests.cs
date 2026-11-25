using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Текст CSV-обмена: формат экспорта (даты, имена, суммы, кавычки)
/// и отчёт импорта («импортировано X, пропущено Y» с причинами).
/// </summary>
public class CsvExchangeTests
{
    private static readonly IReadOnlyDictionary<long, string> Accounts = new Dictionary<long, string>
    {
        [1] = "Наличные",
        [2] = "Карта",
    };

    private static readonly IReadOnlyDictionary<long, string> Categories = new Dictionary<long, string>
    {
        [3] = "Продукты",
        [4] = "Транспорт",
    };

    [Fact]
    public void BuildExportText_FormatsFiveColumns()
    {
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 2, CategoryId = 3, AmountKopecks = 125_050, Date = new DateOnly(2026, 11, 5), Comment = "молоко" },
            new() { Id = 2, AccountId = 1, CategoryId = 4, AmountKopecks = 50_00, Date = new DateOnly(2026, 11, 30), Comment = null },
        };

        string csv = CsvExchange.BuildExportText(transactions, Accounts, Categories);

        string[] lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("05.11.2026;Карта;Продукты;1250,50;молоко", lines[0]);
        Assert.Equal("30.11.2026;Наличные;Транспорт;50,00;", lines[1]);
    }

    [Fact]
    public void BuildExportText_QuotesCommentWithSemicolon_RoundTrips()
    {
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 1, CategoryId = 3, AmountKopecks = 100, Date = new DateOnly(2026, 11, 5), Comment = "хлеб; молоко" },
        };

        string csv = CsvExchange.BuildExportText(transactions, Accounts, Categories);

        // Поле с «;» берётся в кавычки — парсер читает его обратно.
        Assert.Contains("\"хлеб; молоко\"", csv);
        CsvValidationResult result = CsvValidator.Validate(csv, Invert(Accounts), InvertCategories(Categories));
        Assert.Equal("хлеб; молоко", Assert.Single(result.Valid).Comment);
    }

    [Fact]
    public void BuildExportText_DoublesQuotesInsideComment_RoundTrips()
    {
        // ASCII-кавычка в комментарии: поле берётся в кавычки,
        // внутренняя удваивается — обратный разбор восстанавливает её.
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 1, CategoryId = 3, AmountKopecks = 100, Date = new DateOnly(2026, 11, 5), Comment = "соус \"ткемали\"" },
        };

        string csv = CsvExchange.BuildExportText(transactions, Accounts, Categories);

        Assert.Contains("\"соус \"\"ткемали\"\"\"", csv);
        CsvValidationResult result = CsvValidator.Validate(csv, Invert(Accounts), InvertCategories(Categories));
        Assert.Equal("соус \"ткемали\"", Assert.Single(result.Valid).Comment);
    }

    [Fact]
    public void BuildImportReport_WithoutSkipped_IsSingleLine()
    {
        string report = CsvExchange.BuildImportReport(3, Array.Empty<CsvSkippedLine>());

        Assert.Equal("Импортировано 3, пропущено 0.", report);
    }

    [Fact]
    public void BuildImportReport_WithSkipped_ListsReasonsWithLineNumbers()
    {
        var skipped = new List<CsvSkippedLine>
        {
            new(2, "05.11.2026;Наличные;Алкоголь;200,00;", "неизвестная категория: «Алкоголь»"),
            new(5, "32.13.2026;Наличные;Продукты;100,00;", "битая дата: «32.13.2026»"),
        };

        string report = CsvExchange.BuildImportReport(1, skipped);

        Assert.StartsWith("Импортировано 1, пропущено 2.", report);
        Assert.Contains("строка 2: неизвестная категория: «Алкоголь»", report);
        Assert.Contains("строка 5: битая дата: «32.13.2026»", report);
    }

    private static IReadOnlyDictionary<string, long> Invert(IReadOnlyDictionary<long, string> byId) =>
        byId.ToDictionary(pair => pair.Value, pair => pair.Key);

    private static IReadOnlyDictionary<string, IReadOnlyList<long>> InvertCategories(
        IReadOnlyDictionary<long, string> byId) =>
        byId.GroupBy(pair => pair.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<long>)g.Select(pair => pair.Key).ToList());
}
