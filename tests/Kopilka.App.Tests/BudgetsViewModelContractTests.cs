using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты окна бюджетов (шаг 4): заголовки месяцев,
/// форматы ввода лимита (копейки, пробелы, точка-опечатка),
/// переключение выбора категорий и повторное создание бюджета
/// после снятия лимита.
/// </summary>
public class BudgetsViewModelContractTests
{
    private static (BudgetsViewModel ViewModel, FakeBudgetRepository Budgets) Create(string month = "2026-11")
    {
        FakeBudgetRepository budgets = Fakes.Budgets();
        return (new BudgetsViewModel(Fakes.Categories(), budgets, month), budgets);
    }

    // Сид фейков: категория 3 «Продукты» — единственная с признаком лимита.

    [Fact]
    public void MonthTitle_DecemberAndJanuary_AreRussianMonthNames()
    {
        (BudgetsViewModel december, _) = Create("2026-12");
        (BudgetsViewModel january, _) = Create("2027-01");

        Assert.Equal("Декабрь 2026", december.MonthTitle);
        Assert.Equal("Январь 2027", january.MonthTitle);
    }

    [Fact]
    public void SaveLimit_KopecksSpacesAndDot_SavesExactKopecks()
    {
        (BudgetsViewModel viewModel, FakeBudgetRepository budgets) = Create();
        viewModel.SelectedItem = viewModel.Items[0];

        // С копейками: 15 000,50 ₽ = 1 500 050 коп.
        viewModel.LimitText = "15000,50";
        viewModel.SaveLimitCommand.Execute(null);
        Assert.Equal(1_500_050, budgets.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);

        // Пробел-разделитель тысяч: 15 000 ₽.
        viewModel.LimitText = "15 000";
        viewModel.SaveLimitCommand.Execute(null);
        Assert.Equal(15_000_00, budgets.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);

        // Точка — опечатка запятой: снова 1 500 050 коп.
        viewModel.LimitText = "15000.50";
        viewModel.SaveLimitCommand.Execute(null);
        Assert.Equal(1_500_050, budgets.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);

        Assert.Equal(string.Empty, viewModel.ValidationSummary);
        // Пара (категория, месяц) одна: обновление, не дубль.
        Assert.Single(budgets.ListByMonth("2026-11"));
    }

    [Fact]
    public void SelectingCategory_SwitchesLimitText_KeepsSelectionAfterSave()
    {
        FakeCategoryRepository categories = Fakes.Categories();
        long cafeId = categories.Add("Кафе", CategoryKind.Expense, hasLimit: true, sortOrder: 4);
        FakeBudgetRepository budgets = Fakes.Budgets();
        budgets.Set(3, "2026-11", 15_000_00);
        budgets.Set(cafeId, "2026-11", 2_500_00);
        var viewModel = new BudgetsViewModel(categories, budgets, "2026-11");

        Assert.Equal(2, viewModel.Items.Count);

        // Смена выбора обновляет поле лимита значением выбранной категории.
        viewModel.SelectedItem = viewModel.Items[0]; // «Продукты»
        Assert.Equal("15000,00", viewModel.LimitText);

        viewModel.SelectedItem = viewModel.Items[1]; // «Кафе»
        Assert.Equal("2500,00", viewModel.LimitText);

        viewModel.LimitText = "3000";
        viewModel.SaveLimitCommand.Execute(null);

        // Выбор сохранён, подпись обновилась, чужой лимит не тронут.
        Assert.Equal(cafeId, viewModel.SelectedItem!.CategoryId);
        Assert.Equal(3_000_00, budgets.GetByCategoryAndMonth(cafeId, "2026-11")!.LimitKopecks);
        Assert.Equal(15_000_00, budgets.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);
        Assert.Equal("Кафе — 3\u00A0000,00 ₽", viewModel.Items.First(i => i.CategoryId == cafeId).Display);
    }

    [Fact]
    public void RemoveLimit_ThenSaveLimitAgain_CreatesNewBudget()
    {
        (BudgetsViewModel viewModel, FakeBudgetRepository budgets) = Create();
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.LimitText = "15000";
        viewModel.SaveLimitCommand.Execute(null);
        Assert.NotNull(budgets.GetByCategoryAndMonth(3, "2026-11"));

        viewModel.RemoveLimitCommand.Execute(null);
        Assert.Null(budgets.GetByCategoryAndMonth(3, "2026-11"));

        viewModel.LimitText = "12000";
        viewModel.SaveLimitCommand.Execute(null);

        // Бюджет создаётся заново — один на пару (категория, месяц).
        Assert.Equal(12_000_00, budgets.GetByCategoryAndMonth(3, "2026-11")!.LimitKopecks);
        Assert.Single(budgets.ListByMonth("2026-11"));
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
    }
}
