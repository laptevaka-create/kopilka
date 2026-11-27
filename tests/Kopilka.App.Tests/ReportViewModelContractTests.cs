using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты отчёта месяца: доли с округлением до десятых,
/// независимость от фильтра журнала, месяцы на переходе года строятся
/// каждый по своим данным.
/// </summary>
public class ReportViewModelContractTests
{
    // Сид фейков: категории 3 «Продукты», 4 «Транспорт», 5 «Развлечения» —
    // расходы; 1 «Зарплата» — доход.

    [Fact]
    public void Shares_TwoEqualCategories_AreFiftyPercentEach()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 5), null);
        transactions.Add(2, 4, 500_00, new DateOnly(2026, 11, 6), null);

        var report = new ReportViewModel(transactions, Fakes.Categories(), 2026, 11);

        Assert.Equal(2, report.ExpenseRows.Count);
        Assert.All(report.ExpenseRows, row => Assert.Equal("50,0 %", row.ShareText));
        Assert.Equal(1_000_00, report.ExpenseTotalKopecks);
        Assert.Equal("1\u00A0000,00 ₽", report.ExpenseTotalText);
    }

    [Fact]
    public void Shares_OneToOneToTwo_AreQuarterQuarterHalf()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 5), null);
        transactions.Add(1, 4, 100_00, new DateOnly(2026, 11, 6), null);
        transactions.Add(1, 5, 200_00, new DateOnly(2026, 11, 7), null);

        var report = new ReportViewModel(transactions, Fakes.Categories(), 2026, 11);

        // Доли — от общего объёма расходов: 1:1:2 — это 25/25/50 %.
        // (33,3/33,3/66,7 в сумме дали бы 133 % — арифметически невозможно.)
        Assert.Equal(400_00, report.ExpenseTotalKopecks);
        Assert.Equal(3, report.ExpenseRows.Count);
        Assert.Equal("Развлечения", report.ExpenseRows[0].CategoryName);
        Assert.Equal("50,0 %", report.ExpenseRows[0].ShareText);
        Assert.Equal(2, report.ExpenseRows.Count(row => row.ShareText == "25,0 %"));
    }

    [Fact]
    public void Shares_OneToTwo_AreRoundedToTenths()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 5), null);
        transactions.Add(1, 4, 200_00, new DateOnly(2026, 11, 6), null);

        var report = new ReportViewModel(transactions, Fakes.Categories(), 2026, 11);

        // 1/3 и 2/3 с округлением до десятых вверх: 33,3 % и 66,7 %.
        Assert.Equal("Транспорт", report.ExpenseRows[0].CategoryName);
        Assert.Equal("66,7 %", report.ExpenseRows[0].ShareText);
        Assert.Equal("Продукты", report.ExpenseRows[1].CategoryName);
        Assert.Equal("33,3 %", report.ExpenseRows[1].ShareText);
    }

    [Fact]
    public void Report_IgnoresJournalFilter_BuildsAllCategoriesOfMonth()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        FakeCategoryRepository categories = Fakes.Categories();
        transactions.Add(1, 1, 1_000_00, new DateOnly(2026, 11, 5), null); // Зарплата, доход
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 6), null);   // Продукты
        transactions.Add(2, 4, 200_00, new DateOnly(2026, 11, 7), null);   // Транспорт

        // Журнал сужен фильтром по «Транспорту».
        var main = new MainViewModel(
            transactions, Fakes.Accounts(), categories, Fakes.Budgets(),
            Fakes.Accruer(), Fakes.Dialog(result: false), Fakes.Clock());
        main.SelectedFilterCategory = main.FilterCategories.First(c => c.Id == 4);
        Assert.Single(main.Transactions);

        var report = new ReportViewModel(transactions, categories, 2026, 11);

        // Отчёт строится по SumByCategory без фильтра: все категории месяца,
        // фильтр журнала — про просмотр, отчёт — про структуру месяца.
        Assert.Equal(1_000_00, report.IncomeTotalKopecks);
        Assert.Equal(300_00, report.ExpenseTotalKopecks);
        Assert.Equal(2, report.ExpenseRows.Count);
        Assert.Single(report.IncomeRows);
    }

    [Fact]
    public void Report_YearBoundary_EachMonthBuiltFromItsOwnData()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 12, 31), null); // последний день декабря
        transactions.Add(1, 4, 200_00, new DateOnly(2027, 1, 5), null);   // январь следующего года

        var december = new ReportViewModel(transactions, Fakes.Categories(), 2026, 12);
        var january = new ReportViewModel(transactions, Fakes.Categories(), 2027, 1);

        Assert.Equal("Декабрь 2026", december.MonthTitle);
        Assert.Equal(100_00, december.ExpenseTotalKopecks);
        Assert.Equal("Продукты", Assert.Single(december.ExpenseRows).CategoryName);

        Assert.Equal("Январь 2027", january.MonthTitle);
        Assert.Equal(200_00, january.ExpenseTotalKopecks);
        Assert.Equal("Транспорт", Assert.Single(january.ExpenseRows).CategoryName);
    }
}
