using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контрактные тесты валидатора CSV против норматива
/// docs/specs/finance-rules.md: строгий формат даты и граница
/// високосного дня, ISO-дата и «лояльные» суммы как в ручном вводе,
/// пустые комментарии, кавычка в середине некавычного поля,
/// строка из одних «;».
/// </summary>
public class CsvValidatorContractTests
{
    private static readonly IReadOnlyDictionary<string, long> Accounts = new Dictionary<string, long>
    {
        ["Наличные"] = 1,
        ["Карта"] = 2,
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<long>> Categories =
        new Dictionary<string, IReadOnlyList<long>>
        {
            ["Продукты"] = new[] { 3L },
            ["Транспорт"] = new[] { 4L },
            ["Зарплата"] = new[] { 1L },
        };

    [Fact]
    public void Validate_LeapFebruary29AndStrictDayFormat_SplitByDateBoundary()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "29.02.2028;Наличные;Продукты;100,00;\r\n" +
            "29.02.2027;Наличные;Продукты;100,00;\r\n" +
            "2026-11-5;Наличные;Продукты;100,00;",
            Accounts, Categories);

        // 2028 — високосный: 29 февраля валидна.
        ParsedCsvLine leap = Assert.Single(result.Valid);
        Assert.Equal(new DateOnly(2028, 2, 29), leap.Date);

        Assert.Equal(2, result.Skipped.Count);
        Assert.Equal(2, result.Skipped[0].LineNumber);
        Assert.Equal("битая дата: «29.02.2027»", result.Skipped[0].Reason);
        // Формат строгий: однозначный день не принимается.
        Assert.Equal(3, result.Skipped[1].LineNumber);
        Assert.Equal("битая дата: «2026-11-5»", result.Skipped[1].Reason);
    }

    [Fact]
    public void Validate_IsoYearEndAndLooseSums_AreAccepted()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "2026-12-31;Карта;Продукты;1 2 5 0;новогоднее\r\n" +
            "2026-12-31;Карта;Продукты;+100;",
            Accounts, Categories);

        Assert.Equal(2, result.Valid.Count);
        Assert.Empty(result.Skipped);
        Assert.Equal(new DateOnly(2026, 12, 31), result.Valid[0].Date);
        // «1 2 5 0» — пробелы между цифрами, как в ручном вводе: 1250,00 ₽.
        Assert.Equal(125_000, result.Valid[0].AmountKopecks);
        Assert.Equal("новогоднее", result.Valid[0].Comment);
        // «+100» — ведущий плюс допустим: 100,00 ₽.
        Assert.Equal(10_000, result.Valid[1].AmountKopecks);
    }

    [Fact]
    public void Validate_WhitespaceAndEmptyQuotedComment_BecomeNull()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;100,00;   \r\n" +
            "05.11.2026;Наличные;Продукты;100,00;\"\"",
            Accounts, Categories);

        Assert.Equal(2, result.Valid.Count);
        // Комментарий из пробелов и пустое поле в кавычках — пустой
        // комментарий (null), а не строка из пробелов.
        Assert.All(result.Valid, line => Assert.Null(line.Comment));
    }

    [Fact]
    public void Validate_QuoteInsideUnquotedField_RefusedAsUnclosedQuote()
    {
        // Фактическое поведение: кавычка в середине некавычного поля
        // открывает «цитату», которая до конца строки не закрывается —
        // строка пропускается громко с причиной, а не проглатывается
        // молча (данные не портятся незаметно).
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;100,00;аб\"вг",
            Accounts, Categories);

        Assert.Empty(result.Valid);
        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Equal(1, skipped.LineNumber);
        Assert.Equal("незакрытая кавычка", skipped.Reason);
    }

    [Fact]
    public void Validate_LineOfOnlySemicolons_FiveEmptyColumns_DateIsFirstFailure()
    {
        // «;;;;» — пять пустых колонок; проверка начинается с даты,
        // поэтому причина — битая дата, а не «неизвестный счёт: «»».
        CsvValidationResult result = CsvValidator.Validate(";;;;", Accounts, Categories);

        Assert.Empty(result.Valid);
        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Equal(1, skipped.LineNumber);
        Assert.Equal(";;;;", skipped.Line);
        Assert.Equal("битая дата: «»", skipped.Reason);
    }
}
