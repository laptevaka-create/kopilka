using Kopilka.App.Presentation;
using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Панель бюджетов в главной модели (шаг 4): категории с лимитом,
/// статусы от движка, независимость потраченного от фильтра журнала,
/// скрытие при произвольном периоде, текущий день для прошлых
/// и будущих месяцев.
/// </summary>
public class BudgetPanelTests
{
    private static (MainViewModel ViewModel, FakeTransactionRepository Transactions, FakeBudgetRepository Budgets, FakeDialogService Dialog) Create(
        DateTime? clockMoment = null)
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        FakeBudgetRepository budgets = Fakes.Budgets();
        FakeDialogService dialog = Fakes.Dialog(result: true);
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            Fakes.Categories(),
            budgets,
            dialog,
            clockMoment is null ? Fakes.Clock() : Fakes.Clock(clockMoment.Value));
        return (viewModel, transactions, budgets, dialog);
    }

    // Сид фейков: категория 3 «Продукты» — расход с признаком лимита.
    // Часы по умолчанию — 15.11.2026.

    [Fact]
    public void Panel_ShowsLimitCategories_WithComputedStatus()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeBudgetRepository budgets, _) = Create();
        budgets.Set(3, "2026-11", 10_000_00);
        transactions.Add(1, 3, 5_000_00, new DateOnly(2026, 11, 5), null);
        transactions.Add(1, 3, 2_000_00, new DateOnly(2026, 11, 12), null);

        viewModel.Reload();

        BudgetRowViewModel row = Assert.Single(viewModel.Budgets);
        Assert.Equal("Продукты", row.CategoryName);
        Assert.True(row.HasBudget);
        Assert.Equal(7_000_00, row.SpentKopecks);
        Assert.Equal(3_000_00, row.Snapshot!.RemainingKopecks);
        Assert.Equal(BudgetStatus.Ok, row.Snapshot.Status);
        Assert.Equal("в норме", row.StatusText);
        Assert.Equal("7\u00A0000,00 ₽", row.SpentText);
        Assert.Equal("10\u00A0000,00 ₽", row.LimitText);
    }

    [Fact]
    public void Panel_SpentCountsAllMonthTransactions_RegardlessOfJournalFilter()
    {
        // Фильтр журнала — про просмотр; потраченное в бюджете —
        // по всем транзакциям категории за месяц.
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeBudgetRepository budgets, _) = Create();
        budgets.Set(3, "2026-11", 10_000_00);
        transactions.Add(1, 3, 9_000_00, new DateOnly(2026, 11, 5), null);
        transactions.Add(1, 4, 100_00, new DateOnly(2026, 11, 6), null); // другая категория

        // Фильтруем журнал по «Транспорту» — панель бюджетов не меняется.
        viewModel.SelectedFilterCategory = viewModel.FilterCategories.First(c => c.Id == 4);

        BudgetRowViewModel row = Assert.Single(viewModel.Budgets);
        Assert.Equal(9_000_00, row.SpentKopecks);
        Assert.Equal(BudgetStatus.Attention, row.Snapshot!.Status);
        Assert.Equal("внимание", row.StatusText);
    }

    [Fact]
    public void Panel_CategoryWithLimitFlagButNoBudget_ShowsNotSet()
    {
        (MainViewModel viewModel, _, _, _) = Create();

        BudgetRowViewModel row = Assert.Single(viewModel.Budgets);
        Assert.False(row.HasBudget);
        Assert.Equal("не задан", row.LimitText);
        Assert.Equal("не задан", row.StatusText);
        Assert.Equal("—", row.AvailablePerDayText);
    }

    [Fact]
    public void Panel_ExceededBudget_HasNoPerDayAllowance()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeBudgetRepository budgets, _) = Create();
        budgets.Set(3, "2026-11", 5_000_00);
        transactions.Add(1, 3, 6_200_00, new DateOnly(2026, 11, 5), null);

        viewModel.Reload();

        BudgetRowViewModel row = Assert.Single(viewModel.Budgets);
        Assert.Equal(BudgetStatus.Exceeded, row.Snapshot!.Status);
        Assert.Equal("превышен", row.StatusText);
        Assert.Null(row.Snapshot.AvailablePerDayKopecks);
        Assert.Equal("—", row.AvailablePerDayText);
        Assert.Equal("-1\u00A0200,00 ₽", row.RemainingText);
    }

    [Fact]
    public void Panel_HiddenDuringCustomPeriod()
    {
        (MainViewModel viewModel, _, FakeBudgetRepository budgets, _) = Create();
        budgets.Set(3, "2026-11", 10_000_00);
        Assert.True(viewModel.BudgetsVisible);

        viewModel.FilterFromText = "01.11.2026";
        viewModel.FilterToText = "15.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.False(viewModel.BudgetsVisible);
        Assert.Empty(viewModel.Budgets);
        Assert.False(viewModel.ShowBudgetsCommand.CanExecute(null));

        viewModel.ResetPeriodCommand.Execute(null);

        Assert.True(viewModel.BudgetsVisible);
        Assert.Single(viewModel.Budgets);
    }

    [Fact]
    public void Panel_PastMonth_CountsWholeMonthAsElapsed()
    {
        // Прошлый месяц: «доступно в день» — остаток на один день,
        // месяц весь прошёл.
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeBudgetRepository budgets, _) = Create();
        budgets.Set(3, "2026-10", 10_000_00);
        transactions.Add(1, 3, 7_500_00, new DateOnly(2026, 10, 10), null);

        viewModel.PreviousMonthCommand.Execute(null);

        BudgetRowViewModel row = Assert.Single(viewModel.Budgets);
        Assert.Equal(2_500_00, row.Snapshot!.AvailablePerDayKopecks);
    }

    [Fact]
    public void Panel_FutureMonth_CountsFromFirstDay()
    {
        // Будущий месяц: весь месяц впереди, текущий день — первый.
        (MainViewModel viewModel, _, FakeBudgetRepository budgets, _) = Create();
        budgets.Set(3, "2026-12", 6_200_00); // в декабре 31 день

        viewModel.NextMonthCommand.Execute(null);

        BudgetRowViewModel row = Assert.Single(viewModel.Budgets);
        Assert.Equal(200_00, row.Snapshot!.AvailablePerDayKopecks); // 6200,00 / 31
    }

    [Fact]
    public void ShowBudgets_PassesShownMonth_AndRefreshesPanel()
    {
        (MainViewModel viewModel, _, FakeBudgetRepository budgets, FakeDialogService dialog) = Create();

        viewModel.ShowBudgetsCommand.Execute(null);

        Assert.Equal(1, dialog.BudgetsShownCount);
        Assert.Equal("2026-11", dialog.LastBudgetsMonth);

        // Бюджет, заданный в окне, виден в панели после закрытия.
        budgets.Set(3, "2026-11", 10_000_00);
        viewModel.ShowBudgetsCommand.Execute(null);

        BudgetRowViewModel row = Assert.Single(viewModel.Budgets);
        Assert.True(row.HasBudget);
        Assert.Equal(10_000_00, row.LimitKopecks);
    }
}
