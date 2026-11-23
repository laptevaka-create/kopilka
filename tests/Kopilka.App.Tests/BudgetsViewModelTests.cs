using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Окно бюджетов (шаг 4): только расходные категории с признаком
/// лимита, установка и снятие лимита на месяц, валидация ввода,
/// повторное сохранение без дублей.
/// </summary>
public class BudgetsViewModelTests
{
    private static (BudgetsViewModel ViewModel, FakeBudgetRepository Budgets) Create(string month = "2026-11")
    {
        FakeBudgetRepository budgets = Fakes.Budgets();
        var viewModel = new BudgetsViewModel(Fakes.Categories(), budgets, month);
        return (viewModel, budgets);
    }

    [Fact]
    public void Constructor_ListsOnlyExpenseCategoriesWithLimitFlag()
    {
        (BudgetsViewModel viewModel, _) = Create();

        BudgetsViewModel.BudgetItemViewModel item = Assert.Single(viewModel.Items);
        Assert.Equal(3, item.CategoryId); // только «Продукты» имеет признак лимита
        Assert.Null(item.LimitKopecks);
        Assert.Equal("Продукты — не задан", item.Display);
        Assert.Equal("Ноябрь 2026", viewModel.MonthTitle);
    }

    [Fact]
    public void SelectingItem_FillsLimitText()
    {
        (BudgetsViewModel viewModel, FakeBudgetRepository budgets) = Create();
        budgets.Set(3, "2026-11", 15_000_00);
        viewModel.Reload();

        viewModel.SelectedItem = viewModel.Items[0];

        Assert.Equal("15000,00", viewModel.LimitText);
        Assert.True(viewModel.SaveLimitCommand.CanExecute(null));
        Assert.True(viewModel.RemoveLimitCommand.CanExecute(null));
    }

    [Fact]
    public void SaveLimit_Valid_SetsBudget()
    {
        (BudgetsViewModel viewModel, FakeBudgetRepository budgets) = Create();
        viewModel.SelectedItem = viewModel.Items[0];
        viewModel.LimitText = "15000";

        viewModel.SaveLimitCommand.Execute(null);

        Assert.Equal(15_000_00, budgets.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
        Assert.Equal("Продукты — 15\u00A0000,00 ₽", viewModel.Items[0].Display);
        Assert.Equal(3, viewModel.SelectedItem!.CategoryId); // выбор не потерян
    }

    [Fact]
    public void SaveLimit_Twice_UpdatesWithoutDuplicate()
    {
        (BudgetsViewModel viewModel, FakeBudgetRepository budgets) = Create();
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.LimitText = "15000";
        viewModel.SaveLimitCommand.Execute(null);
        viewModel.LimitText = "18000";
        viewModel.SaveLimitCommand.Execute(null);

        Assert.Equal(18_000_00, budgets.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);
        Assert.Single(budgets.ListByMonth("2026-11"));
    }

    [Fact]
    public void SaveLimit_InvalidInput_ShowsReasons()
    {
        (BudgetsViewModel viewModel, FakeBudgetRepository budgets) = Create();
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.LimitText = " ";
        viewModel.SaveLimitCommand.Execute(null);
        Assert.Equal("Введите лимит.", viewModel.ValidationSummary);

        viewModel.LimitText = "много";
        viewModel.SaveLimitCommand.Execute(null);
        Assert.Contains("число", viewModel.ValidationSummary);

        viewModel.LimitText = "0";
        viewModel.SaveLimitCommand.Execute(null);
        Assert.Equal("Лимит должен быть больше нуля.", viewModel.ValidationSummary);

        Assert.Empty(budgets.ListByMonth("2026-11"));
    }

    [Fact]
    public void RemoveLimit_Existing_RemovesAndClearsField()
    {
        (BudgetsViewModel viewModel, FakeBudgetRepository budgets) = Create();
        budgets.Set(3, "2026-11", 15_000_00);
        viewModel.Reload();
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.RemoveLimitCommand.Execute(null);

        Assert.Null(budgets.GetByCategoryAndMonth(3, "2026-11"));
        Assert.Null(viewModel.Items[0].LimitKopecks);
        Assert.Equal(string.Empty, viewModel.LimitText);
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
    }

    [Fact]
    public void RemoveLimit_WhenNotSet_ShowsReason()
    {
        (BudgetsViewModel viewModel, _) = Create();
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.RemoveLimitCommand.Execute(null);

        Assert.Equal("Бюджет на этот месяц не задан.", viewModel.ValidationSummary);
    }

    [Fact]
    public void Budgets_ArePerMonth_OtherMonthNotAffected()
    {
        (BudgetsViewModel viewModel, FakeBudgetRepository budgets) = Create();
        viewModel.SelectedItem = viewModel.Items[0];
        viewModel.LimitText = "15000";
        viewModel.SaveLimitCommand.Execute(null);

        Assert.Null(budgets.GetByCategoryAndMonth(3, "2026-12"));
    }
}
