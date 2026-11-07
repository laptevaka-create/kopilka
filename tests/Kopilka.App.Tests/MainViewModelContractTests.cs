using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты главной модели представления: пустой месяц,
/// месяцы только с доходами или только с расходами, отрицательное
/// сальдо, февраль обычного и високосного года, многократная
/// навигация по кругу и заголовки всех двенадцати месяцев.
/// </summary>
public class MainViewModelContractTests
{
    private static (MainViewModel ViewModel, FakeTransactionRepository Transactions) Create(DateTime clockMoment)
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            Fakes.Categories(),
            Fakes.Dialog(result: false),
            Fakes.Clock(clockMoment));
        return (viewModel, transactions);
    }

    // Сид фейков: счёт 1 «Наличные», 2 «Карта»; категории 1 «Зарплата»,
    // 2 «Прочие доходы» (доходы), 3 «Продукты», 4 «Транспорт» (расходы).

    [Fact]
    public void EmptyMonth_EmptyList_ZeroTotalsAndCorrectTitle()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions) = Create(new DateTime(2026, 11, 15));

        Assert.Empty(viewModel.Transactions);
        Assert.Equal("Ноябрь 2026", viewModel.MonthTitle);
        Assert.Equal(0, viewModel.IncomeKopecks);
        Assert.Equal(0, viewModel.ExpenseKopecks);
        Assert.Equal(0, viewModel.BalanceKopecks);
        Assert.Equal("0,00 ₽", viewModel.IncomeText);
        Assert.Equal("0,00 ₽", viewModel.ExpenseText);
        Assert.Equal("0,00 ₽", viewModel.BalanceText);
    }

    [Fact]
    public void IncomeOnlyMonth_ShowsIncomeTotals_ZeroExpense()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions) = Create(new DateTime(2026, 11, 15));
        transactions.Add(1, 1, 1_250_000_00, new DateOnly(2026, 11, 5), null); // Зарплата, доход
        transactions.Add(2, 2, 5_000_00, new DateOnly(2026, 11, 20), null);   // Прочие доходы

        viewModel.Reload();

        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.Equal(1_255_000_00, viewModel.IncomeKopecks);
        Assert.Equal(0, viewModel.ExpenseKopecks);
        Assert.Equal("1\u00A0255\u00A0000,00 ₽", viewModel.IncomeText);
        Assert.Equal("0,00 ₽", viewModel.ExpenseText);
        Assert.Equal(1_255_000_00, viewModel.BalanceKopecks);
        // Положительное сальдо показывается без знака: «+» только в строках журнала.
        Assert.Equal("1\u00A0255\u00A0000,00 ₽", viewModel.BalanceText);
    }

    [Fact]
    public void ExpenseOnlyMonth_ShowsExpenseTotals_NegativeBalanceWithMinus()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions) = Create(new DateTime(2026, 11, 15));
        transactions.Add(1, 3, 18_450_75, new DateOnly(2026, 11, 7), null); // Продукты, расход
        transactions.Add(1, 4, 2_500_00, new DateOnly(2026, 11, 8), null);  // Транспорт, расход

        viewModel.Reload();

        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.Equal(0, viewModel.IncomeKopecks);
        Assert.Equal(20_950_75, viewModel.ExpenseKopecks);
        Assert.Equal("0,00 ₽", viewModel.IncomeText);
        Assert.Equal("20\u00A0950,75 ₽", viewModel.ExpenseText);
        Assert.Equal(-20_950_75, viewModel.BalanceKopecks);
        // Отрицательное сальдо — с минусом, сумма не теряет копеек.
        Assert.Equal("-20\u00A0950,75 ₽", viewModel.BalanceText);
    }

    [Fact]
    public void Reload_FebruaryOfNonLeapYear_IncludesLastDay_ExcludesNeighbors()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions) = Create(new DateTime(2027, 2, 10));
        transactions.Add(1, 3, 100_00, new DateOnly(2027, 1, 31), null); // соседний месяц слева
        transactions.Add(1, 3, 100_00, new DateOnly(2027, 2, 1), null);
        transactions.Add(1, 3, 100_00, new DateOnly(2027, 2, 28), null); // последний день февраля
        transactions.Add(1, 3, 100_00, new DateOnly(2027, 3, 1), null);  // соседний месяц справа

        viewModel.Reload();

        Assert.Equal("Февраль 2027", viewModel.MonthTitle);
        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.Equal(new DateOnly(2027, 2, 1), viewModel.Transactions[0].Date);
        Assert.Equal(new DateOnly(2027, 2, 28), viewModel.Transactions[1].Date);
    }

    [Fact]
    public void Reload_FebruaryOfLeapYear_IncludesFebruary29()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions) = Create(new DateTime(2028, 2, 10));
        transactions.Add(1, 3, 100_00, new DateOnly(2028, 2, 28), null);
        transactions.Add(1, 3, 100_00, new DateOnly(2028, 2, 29), null); // високосный день
        transactions.Add(1, 3, 100_00, new DateOnly(2028, 3, 1), null);

        viewModel.Reload();

        Assert.Equal("Февраль 2028", viewModel.MonthTitle);
        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.Equal(new DateOnly(2028, 2, 29), viewModel.Transactions[1].Date);
    }

    [Fact]
    public void MonthNavigation_RepeatedSteps_WrapAroundYearBoundaries()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions) = Create(new DateTime(2026, 11, 15));
        transactions.Add(1, 3, 100_00, new DateOnly(2028, 1, 10), null);
        transactions.Add(1, 3, 100_00, new DateOnly(2025, 12, 15), null);

        // 14 шагов вперёд: Ноябрь 2026 → Январь 2028; границу
        // «декабрь → январь» пересекаем дважды (2026→2027 и 2027→2028).
        for (int step = 0; step < 14; step++)
        {
            viewModel.NextMonthCommand.Execute(null);
        }

        Assert.Equal("Январь 2028", viewModel.MonthTitle);
        Assert.Single(viewModel.Transactions);

        // 25 шагов назад: Январь 2028 → Декабрь 2025; границу
        // «январь → декабрь» пересекаем трижды (2028→2027,
        // 2027→2026 и 2026→2025).
        for (int step = 0; step < 25; step++)
        {
            viewModel.PreviousMonthCommand.Execute(null);
        }

        Assert.Equal("Декабрь 2025", viewModel.MonthTitle);
        Assert.Single(viewModel.Transactions);
    }

    [Theory]
    [InlineData(1, "Январь")]
    [InlineData(2, "Февраль")]
    [InlineData(3, "Март")]
    [InlineData(4, "Апрель")]
    [InlineData(5, "Май")]
    [InlineData(6, "Июнь")]
    [InlineData(7, "Июль")]
    [InlineData(8, "Август")]
    [InlineData(9, "Сентябрь")]
    [InlineData(10, "Октябрь")]
    [InlineData(11, "Ноябрь")]
    [InlineData(12, "Декабрь")]
    public void MonthTitle_AllTwelveMonths_AreRussianNames(int month, string expectedName)
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions) = Create(new DateTime(2026, month, 15));
        transactions.Add(1, 3, 100_00, new DateOnly(2026, month, 10), null);

        viewModel.Reload();

        Assert.Equal($"{expectedName} 2026", viewModel.MonthTitle);
        Assert.Single(viewModel.Transactions);
    }
}
