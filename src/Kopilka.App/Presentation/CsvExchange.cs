using System.Globalization;
using System.Text;
using Kopilka.Core.Domain;
using Kopilka.Core.Engines;

namespace Kopilka.App.Presentation;

/// <summary>
/// Сборка текста CSV для экспорта и отчёта импорта. Разделитель —
/// точка с запятой, кодировка UTF-8. Поле, содержащее «;» или кавычку,
/// берётся в кавычки с удвоением внутренних — файл читается обратно
/// без потерь. Формат нормирован в docs/specs/finance-rules.md.
/// </summary>
public static class CsvExchange
{
    private static readonly CultureInfo Russian = new("ru-RU");

    /// <summary>
    /// Текст CSV для транзакций месяца: строки
    /// «дата;счёт;категория;сумма;комментарий», без заголовка.
    /// </summary>
    public static string BuildExportText(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyDictionary<long, string> accountNames,
        IReadOnlyDictionary<long, string> categoryNames)
    {
        var builder = new StringBuilder();
        foreach (Transaction transaction in transactions)
        {
            string sum = (transaction.AmountKopecks / 100m).ToString("0.00", Russian);
            builder
                .Append(Escape(CalendarText.FormatDate(transaction.Date))).Append(';')
                .Append(Escape(accountNames[transaction.AccountId])).Append(';')
                .Append(Escape(categoryNames[transaction.CategoryId])).Append(';')
                .Append(Escape(sum)).Append(';')
                .Append(Escape(transaction.Comment ?? string.Empty))
                .AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>Отчёт импорта: «Импортировано X, пропущено Y.» и причины по строкам.</summary>
    public static string BuildImportReport(int imported, IReadOnlyList<CsvSkippedLine> skipped)
    {
        var builder = new StringBuilder();
        builder.Append($"Импортировано {imported}, пропущено {skipped.Count}.");
        if (skipped.Count > 0)
        {
            builder.AppendLine().AppendLine().Append("Пропущенные строки:");
            foreach (CsvSkippedLine line in skipped)
            {
                builder.AppendLine().Append($"  строка {line.LineNumber}: {line.Reason}");
            }
        }

        return builder.ToString();
    }

    private static string Escape(string field) =>
        field.Contains(';') || field.Contains('"')
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;
}
