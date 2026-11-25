using System.Globalization;
using System.Text;

namespace Kopilka.Core.Engines;

/// <summary>Распарсенная строка CSV: поля транзакции, готовые к сохранению.</summary>
public sealed record ParsedCsvLine(
    int LineNumber,
    long AccountId,
    long CategoryId,
    long AmountKopecks,
    DateOnly Date,
    string? Comment);

/// <summary>Пропущенная строка CSV: номер, текст и причина.</summary>
public sealed record CsvSkippedLine(int LineNumber, string Line, string Reason);

/// <summary>Результат проверки файла: валидные строки и пропущенные с причинами.</summary>
public sealed record CsvValidationResult(
    IReadOnlyList<ParsedCsvLine> Valid,
    IReadOnlyList<CsvSkippedLine> Skipped);

/// <summary>
/// Валидатор CSV-обмена: построчная проверка без исключений наружу —
/// результат всегда отчёт «валидные строки + пропущенные с причинами»,
/// битый файл не прерывает работу. Формат нормирован
/// в docs/specs/finance-rules.md.
/// </summary>
public static class CsvValidator
{
    private const int ColumnCount = 5;
    private static readonly CultureInfo Russian = new("ru-RU");

    /// <summary>
    /// Проверяет текст CSV: каждая непустая строка — пять колонок
    /// «дата;счёт;категория;сумма;комментарий». Пустые строки
    /// игнорируются (не ошибки), номера строк — с единицы.
    /// </summary>
    public static CsvValidationResult Validate(
        string text,
        IReadOnlyDictionary<string, long> accountsByName,
        IReadOnlyDictionary<string, IReadOnlyList<long>> categoriesByName)
    {
        var valid = new List<ParsedCsvLine>();
        var skipped = new List<CsvSkippedLine>();

        // BOM не должен портить первую строку: чтение файла его снимает,
        // но текст может прийти и напрямую.
        text = text.TrimStart('\uFEFF');

        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (line.Trim().Length == 0)
            {
                continue;
            }

            TryValidateLine(line, i + 1, accountsByName, categoriesByName, valid, skipped);
        }

        return new CsvValidationResult(valid, skipped);
    }

    private static void TryValidateLine(
        string line,
        int lineNumber,
        IReadOnlyDictionary<string, long> accountsByName,
        IReadOnlyDictionary<string, IReadOnlyList<long>> categoriesByName,
        List<ParsedCsvLine> valid,
        List<CsvSkippedLine> skipped)
    {
        string[]? columns = SplitCsvLine(line);
        if (columns is null)
        {
            skipped.Add(new CsvSkippedLine(lineNumber, line, "незакрытая кавычка"));
            return;
        }

        if (columns.Length != ColumnCount)
        {
            skipped.Add(new CsvSkippedLine(
                lineNumber, line, $"неверное число колонок: {columns.Length} вместо {ColumnCount}"));
            return;
        }

        if (!DateOnly.TryParseExact(
                columns[0].Trim(),
                new[] { "dd.MM.yyyy", "yyyy-MM-dd" },
                Russian,
                DateTimeStyles.None,
                out DateOnly date))
        {
            skipped.Add(new CsvSkippedLine(lineNumber, line, $"битая дата: «{columns[0]}»"));
            return;
        }

        string accountName = columns[1].Trim();
        if (!accountsByName.TryGetValue(accountName, out long accountId))
        {
            skipped.Add(new CsvSkippedLine(lineNumber, line, $"неизвестный счёт: «{accountName}»"));
            return;
        }

        string categoryName = columns[2].Trim();
        if (!categoriesByName.TryGetValue(categoryName, out IReadOnlyList<long>? categoryIds))
        {
            skipped.Add(new CsvSkippedLine(lineNumber, line, $"неизвестная категория: «{categoryName}»"));
            return;
        }

        // Имя уникально внутри типа, но «Прочие» может быть и доходом,
        // и расходом: без колонки типа строка неоднозначна — честный отказ.
        if (categoryIds.Count > 1)
        {
            skipped.Add(new CsvSkippedLine(
                lineNumber, line,
                $"неоднозначная категория: «{categoryName}» — имя есть и в доходах, и в расходах"));
            return;
        }

        if (!MoneyParser.TryParseRubles(columns[3], out long kopecks, out bool tooBig))
        {
            skipped.Add(new CsvSkippedLine(
                lineNumber, line,
                tooBig ? $"слишком большая сумма: «{columns[3]}»" : $"битая сумма: «{columns[3]}»"));
            return;
        }

        if (kopecks <= 0)
        {
            skipped.Add(new CsvSkippedLine(lineNumber, line, $"неположительная сумма: {columns[3]}"));
            return;
        }

        string comment = columns[4].Trim();
        valid.Add(new ParsedCsvLine(
            lineNumber, accountId, categoryIds[0], kopecks, date,
            comment.Length == 0 ? null : comment));
    }

    /// <summary>
    /// Делит строку по «;» с учётом кавычек: поле в кавычках может
    /// содержать точку с запятой, внутренние кавычки удвоены.
    /// Незакрытая кавычка — null.
    /// </summary>
    private static string[]? SplitCsvLine(string line)
    {
        var columns = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char symbol = line[i];
            if (inQuotes)
            {
                if (symbol == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(symbol);
                }
            }
            else if (symbol == '"')
            {
                inQuotes = true;
            }
            else if (symbol == ';')
            {
                columns.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(symbol);
            }
        }

        if (inQuotes)
        {
            return null;
        }

        columns.Add(current.ToString());
        return columns.ToArray();
    }
}
