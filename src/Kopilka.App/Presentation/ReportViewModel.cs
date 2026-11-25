using System.Globalization;
using Kopilka.Core.Data;
using Kopilka.Core.Domain;

namespace Kopilka.App.Presentation;

/// <summary>
/// Отчёт месяца: по каждой категории с движением — сумма и доля
/// в общем объёме доходов или расходов (ФТ-5.1), итоги по каждой
/// части и сальдо. Строки — по убыванию суммы. Данные собираются
/// из репозиториев, окно только показывает.
/// </summary>
public sealed class ReportViewModel
{
    private static readonly CultureInfo Russian = new("ru-RU");

    public ReportViewModel(
        ITransactionRepository transactions,
        ICategoryRepository categories,
        int year,
        int month)
    {
        MonthTitle = CalendarText.MonthTitle(year, month);

        DateOnly from = new(year, month, 1);
        DateOnly to = new(year, month, DateTime.DaysInMonth(year, month));

        IReadOnlyDictionary<long, long> sums = transactions.SumByCategory(from, to);
        List<Category> categoriesWithSums = categories.ListAll()
            .Where(c => sums.ContainsKey(c.Id))
            .ToList();

        long incomeTotal = categoriesWithSums
            .Where(c => c.Kind == CategoryKind.Income)
            .Sum(c => sums[c.Id]);
        long expenseTotal = categoriesWithSums
            .Where(c => c.Kind == CategoryKind.Expense)
            .Sum(c => sums[c.Id]);

        IncomeRows = BuildRows(categoriesWithSums, sums, CategoryKind.Income, incomeTotal);
        ExpenseRows = BuildRows(categoriesWithSums, sums, CategoryKind.Expense, expenseTotal);
        IncomeTotalKopecks = incomeTotal;
        ExpenseTotalKopecks = expenseTotal;
    }

    /// <summary>«Ноябрь 2026» — для заголовка окна.</summary>
    public string MonthTitle { get; }

    /// <summary>Категории доходов: сумма и доля в доходах.</summary>
    public IReadOnlyList<ReportRowViewModel> IncomeRows { get; }

    /// <summary>Категории расходов: сумма и доля в расходах.</summary>
    public IReadOnlyList<ReportRowViewModel> ExpenseRows { get; }

    public long IncomeTotalKopecks { get; }

    public long ExpenseTotalKopecks { get; }

    public string IncomeTotalText => MoneyText.Format(IncomeTotalKopecks);

    public string ExpenseTotalText => MoneyText.Format(ExpenseTotalKopecks);

    /// <summary>Сальдо месяца: доходы минус расходы.</summary>
    public string BalanceText => MoneyText.Format(IncomeTotalKopecks - ExpenseTotalKopecks);

    private static IReadOnlyList<ReportRowViewModel> BuildRows(
        List<Category> categoriesWithSums,
        IReadOnlyDictionary<long, long> sums,
        CategoryKind kind,
        long total) =>
        categoriesWithSums
            .Where(c => c.Kind == kind)
            .OrderByDescending(c => sums[c.Id])
            .ThenBy(c => c.Name)
            .Select(c => new ReportRowViewModel(
                c.Name,
                MoneyText.Format(sums[c.Id]),
                ShareText(sums[c.Id], total)))
            .ToList();

    /// <summary>«66,7 %» — доля суммы в итогe; без итога — прочерк.</summary>
    private static string ShareText(long sum, long total)
    {
        if (total <= 0)
        {
            return "—";
        }

        decimal share = Math.Round(sum * 100m / total, 1, MidpointRounding.AwayFromZero);
        return share.ToString("0.0", Russian) + " %";
    }

    /// <summary>Строка отчёта: категория, сумма, доля.</summary>
    public sealed record ReportRowViewModel(string CategoryName, string SumText, string ShareText);
}
