using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Валидатор CSV против норматива docs/specs/finance-rules.md:
/// форматы даты и суммы, разрешение счёта и категории по имени,
/// неоднозначное имя категории, причины отказов, номера строк,
/// кавычки, BOM и CRLF.
/// </summary>
public class CsvValidatorTests
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
            ["Прочие"] = new[] { 10L, 11L }, // имя и в доходах, и в расходах
        };

    [Fact]
    public void ValidLine_ParsesExactly()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;1250,50;молоко и хлеб",
            Accounts, Categories);

        ParsedCsvLine line = Assert.Single(result.Valid);
        Assert.Empty(result.Skipped);
        Assert.Equal(1, line.LineNumber);
        Assert.Equal(1, line.AccountId);
        Assert.Equal(3, line.CategoryId);
        Assert.Equal(125_050, line.AmountKopecks);
        Assert.Equal(new DateOnly(2026, 11, 5), line.Date);
        Assert.Equal("молоко и хлеб", line.Comment);
    }

    [Fact]
    public void ValidLine_IsoDateAndQuotedCommentWithSemicolon()
    {
        // Экспорт кавычит поле с «;»: круглый обмен не теряет данные.
        CsvValidationResult result = CsvValidator.Validate(
            "2026-11-05;Карта;Продукты;100,00;\"хлеб; молоко\"",
            Accounts, Categories);

        ParsedCsvLine line = Assert.Single(result.Valid);
        Assert.Equal(2, line.AccountId);
        Assert.Equal("хлеб; молоко", line.Comment);
    }

    [Fact]
    public void ValidLine_EmptyComment_BecomesNull()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;100,00;",
            Accounts, Categories);

        Assert.Null(Assert.Single(result.Valid).Comment);
    }

    [Fact]
    public void TooFewOrTooManyColumns_AreSkippedWithReason()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;100,00\r\n" +
            "05.11.2026;Наличные;Продукты;100,00;лишнее;поле",
            Accounts, Categories);

        Assert.Empty(result.Valid);
        Assert.Equal(2, result.Skipped.Count);
        Assert.Contains("неверное число колонок: 4 вместо 5", result.Skipped[0].Reason);
        Assert.Contains("неверное число колонок: 6 вместо 5", result.Skipped[1].Reason);
        Assert.Equal(1, result.Skipped[0].LineNumber);
        Assert.Equal(2, result.Skipped[1].LineNumber);
    }

    [Fact]
    public void BadDate_IsSkippedWithReason()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "32.13.2026;Наличные;Продукты;100,00;",
            Accounts, Categories);

        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Equal("битая дата: «32.13.2026»", skipped.Reason);
    }

    [Fact]
    public void UnknownAccount_IsSkippedWithReason()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Кошелёк;Продукты;100,00;",
            Accounts, Categories);

        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Equal("неизвестный счёт: «Кошелёк»", skipped.Reason);
    }

    [Fact]
    public void UnknownCategory_IsSkippedWithReason()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Алкоголь;100,00;",
            Accounts, Categories);

        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Equal("неизвестная категория: «Алкоголь»", skipped.Reason);
    }

    [Fact]
    public void AmbiguousCategory_IsSkippedWithReason()
    {
        // «Прочие» есть и в доходах, и в расходах: без колонки типа
        // строку сохранить нельзя — честный отказ с причиной.
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Прочие;100,00;",
            Accounts, Categories);

        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Contains("неоднозначная категория", skipped.Reason);
        Assert.Contains("Прочие", skipped.Reason);
    }

    [Fact]
    public void NotNumberSum_IsSkippedWithReason()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;много;",
            Accounts, Categories);

        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Equal("битая сумма: «много»", skipped.Reason);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-100,50")]
    public void NonPositiveSum_IsSkippedWithReason(string sum)
    {
        CsvValidationResult result = CsvValidator.Validate(
            $"05.11.2026;Наличные;Продукты;{sum};",
            Accounts, Categories);

        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.StartsWith("неположительная сумма", skipped.Reason);
    }

    [Fact]
    public void TooBigSum_IsSkippedWithReason()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;99999999999999999999;",
            Accounts, Categories);

        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.StartsWith("слишком большая сумма", skipped.Reason);
    }

    [Fact]
    public void EmptyLines_IgnoredButLineNumbersPreserved()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "\r\n" +
            "05.11.2026;Наличные;Продукты;100,00;\r\n" +
            "   \r\n" +
            "05.11.2026;Наличные;Алкоголь;100,00;\r\n",
            Accounts, Categories);

        Assert.DoesNotContain(result.Skipped, s => s.Reason.Contains("колонок"));
        ParsedCsvLine valid = Assert.Single(result.Valid);
        Assert.Equal(2, valid.LineNumber);
        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Equal(4, skipped.LineNumber);
    }

    [Fact]
    public void BomAndCrlf_DoNotBreakFirstLine()
    {
        string text = "\uFEFF05.11.2026;Наличные;Продукты;100,00;\r\n" +
                      "06.11.2026;Карта;Транспорт;50,00;\r\n";

        CsvValidationResult result = CsvValidator.Validate(text, Accounts, Categories);

        Assert.Equal(2, result.Valid.Count);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void UnclosedQuote_IsSkippedWithReason()
    {
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;100,00;\"незакрытый",
            Accounts, Categories);

        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Equal("незакрытая кавычка", skipped.Reason);
    }

    [Fact]
    public void PairedQuotesInsideUnquotedField_AreStripped_AsDocumented()
    {
        // Правило лояльного CSV (finance-rules.md): кавычка внутри
        // некавычного поля открывает секцию кавычек; парная закрывает
        // и снимается. Экспорт такие поля оборачивает целиком, поэтому
        // потеря возможна только при ручной правке файла.
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;100,00;аб\"вг\"",
            Accounts, Categories);

        ParsedCsvLine line = Assert.Single(result.Valid);
        Assert.Empty(result.Skipped);
        Assert.Equal("абвг", line.Comment);
    }

    [Fact]
    public void EmptyText_NoValidNoSkipped()
    {
        CsvValidationResult result = CsvValidator.Validate(string.Empty, Accounts, Categories);

        Assert.Empty(result.Valid);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void MixedFile_KeepsValidLinesAndSkipsBrokenOnes()
    {
        // Битая строка не прерывает разбор: валидные до и после неё
        // сохраняются, номера строк — сквозные.
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;100,00;\r\n" +
            "05.11.2026;Наличные;Алкоголь;200,00;\r\n" +
            "06.11.2026;Карта;Транспорт;50,00;проезд\r\n",
            Accounts, Categories);

        Assert.Equal(2, result.Valid.Count);
        CsvSkippedLine skipped = Assert.Single(result.Skipped);
        Assert.Equal(2, skipped.LineNumber);
        Assert.Equal("05.11.2026;Наличные;Алкоголь;200,00;", skipped.Line);
    }

    [Fact]
    public void SumWithSpacesAndDot_IsAccepted()
    {
        // Пробелы-разделители тысяч и точка-опечатка допустимы —
        // правила те же, что у ввода суммы из интерфейса.
        CsvValidationResult result = CsvValidator.Validate(
            "05.11.2026;Наличные;Продукты;1 250.50;",
            Accounts, Categories);

        Assert.Equal(125_050, Assert.Single(result.Valid).AmountKopecks);
    }
}
