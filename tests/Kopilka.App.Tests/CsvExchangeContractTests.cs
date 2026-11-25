using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты круглого обмена CSV: комментарии со «;», кавычкой
/// и гильотинами возвращаются дословно; половина копейки при импорте
/// округляется вверх, а экспорт пишет нормализованную сумму.
/// </summary>
public class CsvExchangeContractTests
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
    public void BuildExportText_CommentsWithSemicolonQuoteAndGuillemets_RoundTripVerbatim()
    {
        var transactions = new List<Transaction>
        {
            new()
            {
                Id = 1, AccountId = 1, CategoryId = 3, AmountKopecks = 100_00,
                Date = new DateOnly(2026, 11, 5), Comment = "хлеб; \"молоко\" »»",
            },
            new()
            {
                Id = 2, AccountId = 2, CategoryId = 4, AmountKopecks = 50_00,
                Date = new DateOnly(2026, 11, 6), Comment = "только » и « без разделителей",
            },
        };

        string csv = CsvExchange.BuildExportText(transactions, Accounts, Categories);

        // Поле с «;» и кавычкой — в кавычках, внутренние удвоены.
        Assert.Contains("\"хлеб; \"\"молоко\"\" »»\"", csv);
        // Гильотины кавычками не считаются: поле остаётся без кавычек.
        Assert.Contains(";только » и « без разделителей", csv);

        CsvValidationResult result = CsvValidator.Validate(csv, InvertAccounts(), InvertCategories());

        Assert.Equal(2, result.Valid.Count);
        Assert.Empty(result.Skipped);
        Assert.Equal("хлеб; \"молоко\" »»", result.Valid[0].Comment);
        Assert.Equal("только » и « без разделителей", result.Valid[1].Comment);
    }

    [Fact]
    public void ValidateHalfKopeck_RoundsHalfUp_ThenExportWritesNormalizedSum()
    {
        // «0,005» ₽ — половина копейки: округление половины от нуля
        // вверх даёт 1 копейку.
        CsvValidationResult halfKopeck = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;0,005;", InvertAccounts(), InvertCategories());
        ParsedCsvLine parsed = Assert.Single(halfKopeck.Valid);
        Assert.Equal(1, parsed.AmountKopecks);

        // Экспорт нормализует сумму до «0,01»: обратный импорт — та же копейка.
        string csv = CsvExchange.BuildExportText(
            new List<Transaction>
            {
                new()
                {
                    Id = 1, AccountId = 1, CategoryId = 3, AmountKopecks = parsed.AmountKopecks,
                    Date = parsed.Date, Comment = null,
                },
            },
            Accounts, Categories);
        Assert.Contains(";0,01;", csv);
        ParsedCsvLine reimported = Assert.Single(
            CsvValidator.Validate(csv, InvertAccounts(), InvertCategories()).Valid);
        Assert.Equal(1, reimported.AmountKopecks);

        // «0,004» ₽ — 0,4 копейки: округляется в ноль, нулевых сумм нет.
        CsvValidationResult nearlyZero = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;0,004;", InvertAccounts(), InvertCategories());
        Assert.Equal("неположительная сумма: 0,004", Assert.Single(nearlyZero.Skipped).Reason);
    }

    private static IReadOnlyDictionary<string, long> InvertAccounts() =>
        Accounts.ToDictionary(pair => pair.Value, pair => pair.Key);

    private static IReadOnlyDictionary<string, IReadOnlyList<long>> InvertCategories() =>
        Categories.GroupBy(pair => pair.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<long>)g.Select(pair => pair.Key).ToList());
}
