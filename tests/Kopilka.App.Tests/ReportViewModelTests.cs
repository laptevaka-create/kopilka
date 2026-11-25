using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Отчёт месяца (шаг 5): секции доходов и расходов, доли, порядок
/// строк, пустой месяц, итоги и сальдо.
/// </summary>
public class ReportViewModelTests
{
    private static (ReportViewModel ViewModel, FakeTransactionRepository Transactions) Create(
        int year = 2026, int month = 11)
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        var viewModel = new ReportViewModel(transactions, Fakes.Categories(), year, month);
        return (viewModel, transactions);
    }

    [Fact]
    public void MonthWithIncomeAndExpense_TwoSectionsWithShares()
    {
        (ReportViewModel viewModel, FakeTransactionRepository transactions) = Create();
        transactions.Add(1, 1, 100_000_00, new DateOnly(2026, 11, 5), null);  // Зарплата
        transactions.Add(1, 3, 600_00, new DateOnly(2026, 11, 6), null);     // Продукты
        transactions.Add(2, 4, 300_00, new DateOnly(2026, 11, 7), null);     // Транспорт

        var report = new ReportViewModel(transactions, Fakes.Categories(), 2026, 11);

        var income = Assert.Single(report.IncomeRows);
        Assert.Equal("Зарплата", income.CategoryName);
        Assert.Equal("100,0 %", income.ShareText);

        Assert.Equal(2, report.ExpenseRows.Count);
        // По убыванию суммы: Продукты (600), затем Транспорт (300).
        Assert.Equal("Продукты", report.ExpenseRows[0].CategoryName);
        Assert.Equal("66,7 %", report.ExpenseRows[0].ShareText);
        Assert.Equal("Транспорт", report.ExpenseRows[1].CategoryName);
        Assert.Equal("33,3 %", report.ExpenseRows[1].ShareText);

        Assert.Equal(100_000_00, report.IncomeTotalKopecks);
        Assert.Equal(900_00, report.ExpenseTotalKopecks);
        Assert.Equal("100\u00A0000,00 ₽", report.IncomeTotalText);
        Assert.Equal("900,00 ₽", report.ExpenseTotalText);
    }

    [Fact]
    public void EqualShares_RoundToOneDecimal()
    {
        (ReportViewModel viewModel, FakeTransactionRepository transactions) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 6), null);
        transactions.Add(1, 4, 100_00, new DateOnly(2026, 11, 7), null);
        transactions.Add(1, 5, 100_00, new DateOnly(2026, 11, 8), null);

        var report = new ReportViewModel(transactions, Fakes.Categories(), 2026, 11);

        Assert.Equal(3, report.ExpenseRows.Count);
        Assert.All(report.ExpenseRows, row => Assert.Equal("33,3 %", row.ShareText));
    }

    [Fact]
    public void EmptyMonth_NoRows_ZeroTotals()
    {
        (ReportViewModel viewModel, _) = Create();

        Assert.Empty(viewModel.IncomeRows);
        Assert.Empty(viewModel.ExpenseRows);
        Assert.Equal(0, viewModel.IncomeTotalKopecks);
        Assert.Equal(0, viewModel.ExpenseTotalKopecks);
        Assert.Equal("0,00 ₽", viewModel.BalanceText);
    }

    [Fact]
    public void OtherMonthTransactions_DoNotLeakIntoReport()
    {
        (ReportViewModel viewModel, FakeTransactionRepository transactions) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 10, 31), null);
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 12, 1), null);

        var report = new ReportViewModel(transactions, Fakes.Categories(), 2026, 11);

        Assert.Empty(report.ExpenseRows);
        Assert.Equal(0, report.ExpenseTotalKopecks);
    }

    [Fact]
    public void MonthTitle_IsRussian()
    {
        (ReportViewModel viewModel, _) = Create(month: 2);

        Assert.Equal("Февраль 2026", viewModel.MonthTitle);
    }
}
