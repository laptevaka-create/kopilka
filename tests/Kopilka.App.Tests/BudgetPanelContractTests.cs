using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты панели бюджетов (шаг 4): пересчёт по своему месяцу
/// при навигации, обновление потраченного после удаления и добавления
/// транзакции, реакция строки панели на переименование и удаление
/// категории с признаком лимита.
/// </summary>
public class BudgetPanelContractTests
{
    private static (MainViewModel ViewModel, FakeTransactionRepository Transactions, FakeCategoryRepository Categories, FakeBudgetRepository Budgets, FakeDialogService Dialog) Create(
        DateTime? clockMoment = null)
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        FakeCategoryRepository categories = Fakes.Categories();
        FakeBudgetRepository budgets = Fakes.Budgets();
        FakeDialogService dialog = Fakes.Dialog(result: true);
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            categories,
            budgets,
            dialog,
            clockMoment is null ? Fakes.Clock() : Fakes.Clock(clockMoment.Value));
        return (viewModel, transactions, categories, budgets, dialog);
    }

    // Сид фейков: категория 3 «Продукты» — расход с признаком лимита.
    // Часы по умолчанию — 15 ноября 2026.

    [Fact]
    public void MonthNavigation_RecalculatesBudgetPanel_ForItsOwnMonth()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _, FakeBudgetRepository budgets, _) = Create();
        budgets.Set(3, "2026-11", 10_000_00);
        transactions.Add(1, 3, 7_000_00, new DateOnly(2026, 11, 5), null);
        budgets.Set(3, "2026-12", 5_000_00);
        transactions.Add(1, 3, 1_000_00, new DateOnly(2026, 12, 3), null);
        budgets.Set(3, "2027-01", 8_000_00);
        transactions.Add(1, 3, 8_000_00, new DateOnly(2027, 1, 10), null);

        viewModel.Reload();
        BudgetRowViewModel november = Assert.Single(viewModel.Budgets);
        Assert.Equal(10_000_00, november.LimitKopecks);
        Assert.Equal(7_000_00, november.SpentKopecks);

        viewModel.NextMonthCommand.Execute(null); // декабрь
        BudgetRowViewModel december = Assert.Single(viewModel.Budgets);
        Assert.Equal(5_000_00, december.LimitKopecks);
        Assert.Equal(1_000_00, december.SpentKopecks);

        // Через границу года: январь 2027, бюджет исчерпан.
        viewModel.NextMonthCommand.Execute(null);
        BudgetRowViewModel january = Assert.Single(viewModel.Budgets);
        Assert.Equal(8_000_00, january.LimitKopecks);
        Assert.Equal(8_000_00, january.SpentKopecks);
        Assert.Equal(BudgetStatus.Exceeded, january.Snapshot!.Status);

        // Назад: декабрь со своими бюджетом и потраченным.
        viewModel.PreviousMonthCommand.Execute(null);
        BudgetRowViewModel backToDecember = Assert.Single(viewModel.Budgets);
        Assert.Equal(5_000_00, backToDecember.LimitKopecks);
        Assert.Equal(1_000_00, backToDecember.SpentKopecks);
    }

    [Fact]
    public void DeleteTransaction_RecalculatesPanelSpent_AndStatusFlips()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _, FakeBudgetRepository budgets, FakeDialogService dialog) = Create();
        budgets.Set(3, "2026-11", 9_000_00);
        transactions.Add(1, 3, 8_000_00, new DateOnly(2026, 11, 5), null);
        transactions.Add(1, 3, 1_000_00, new DateOnly(2026, 11, 6), null);
        viewModel.Reload();
        Assert.Equal(BudgetStatus.Exceeded, Assert.Single(viewModel.Budgets).Snapshot!.Status);

        viewModel.SelectedTransaction = viewModel.Transactions[0]; // 8 000,00 ₽ от 5 ноября
        dialog.ConfirmResult = true;

        viewModel.DeleteTransactionCommand.Execute(null);

        BudgetRowViewModel row = Assert.Single(viewModel.Budgets);
        Assert.Equal(1_000_00, row.SpentKopecks);
        Assert.Equal(BudgetStatus.Ok, row.Snapshot!.Status);
        Assert.Equal(8_000_00, row.Snapshot.RemainingKopecks);
    }

    [Fact]
    public void AddTransaction_AfterDialogSaved_UpdatesPanelSpent()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        FakeBudgetRepository budgets = Fakes.Budgets();
        budgets.Set(3, "2026-11", 10_000_00);
        transactions.Add(1, 3, 5_000_00, new DateOnly(2026, 11, 5), null);
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            Fakes.Categories(),
            budgets,
            new AddTransactionDialog(transactions),
            Fakes.Clock());

        BudgetRowViewModel before = Assert.Single(viewModel.Budgets);
        Assert.Equal(5_000_00, before.SpentKopecks);
        Assert.Equal(BudgetStatus.Ok, before.Snapshot!.Status);

        viewModel.AddTransactionCommand.Execute(null); // диалог добавил 3 000,00 ₽

        // 8 000,00 ₽ из 10 000,00 ₽ — ровно 80 %: порог включительный.
        BudgetRowViewModel after = Assert.Single(viewModel.Budgets);
        Assert.Equal(8_000_00, after.SpentKopecks);
        Assert.Equal(BudgetStatus.Attention, after.Snapshot!.Status);
        Assert.Equal(2_000_00, after.Snapshot.RemainingKopecks);
        Assert.Equal(12_500, after.Snapshot.AvailablePerDayKopecks); // на 16 дней
    }

    [Fact]
    public void ShowCategories_AfterCategoryRenamed_UpdatesBudgetRowName()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeCategoryRepository categories, FakeBudgetRepository budgets, _) = Create();
        budgets.Set(3, "2026-11", 10_000_00);
        transactions.Add(1, 3, 5_000_00, new DateOnly(2026, 11, 5), null);
        viewModel.Reload();
        Assert.Equal("Продукты", Assert.Single(viewModel.Budgets).CategoryName);

        // Категорию переименовали в окне справочника — признак лимита сохранён.
        categories.Update(new Category
        {
            Id = 3,
            Name = "Еда",
            Kind = CategoryKind.Expense,
            HasLimit = true,
            SortOrder = 0,
        });
        viewModel.ShowCategoriesCommand.Execute(null);

        BudgetRowViewModel renamed = Assert.Single(viewModel.Budgets);
        Assert.Equal("Еда", renamed.CategoryName);
        Assert.True(renamed.HasBudget);
        Assert.Equal(10_000_00, renamed.LimitKopecks);
    }

    [Fact]
    public void ShowCategories_AfterDeletingUnusedLimitCategory_RemovesBudgetRow()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeCategoryRepository categories, _, FakeDialogService dialog) = Create();
        transactions.Add(1, 4, 100_00, new DateOnly(2026, 11, 5), null); // траты не в «Продуктах»
        viewModel.Reload();
        Assert.Single(viewModel.Budgets); // «Продукты — не задан»: признак лимита есть

        // Неиспользуемая категория (без транзакций и бюджета) удалена
        // через окно справочника.
        Assert.Equal(DeleteResult.Deleted, categories.Delete(3));
        viewModel.ShowCategoriesCommand.Execute(null);

        Assert.Empty(viewModel.Budgets);
        Assert.Equal(1, dialog.CategoriesShownCount);
    }

    /// <summary>Диалог добавления, который сохраняет транзакцию — как настоящий с валидной формой.</summary>
    private sealed class AddTransactionDialog(FakeTransactionRepository transactions) : IDialogService
    {
        public bool ShowAddTransaction()
        {
            transactions.Add(1, 3, 3_000_00, new DateOnly(2026, 11, 20), "из диалога");
            return true;
        }

        public bool ShowEditTransaction(long transactionId) => false;

        public void ShowCategories()
        {
        }

        public void ShowBudgets(string month)
        {
        }

        public bool Confirm(string title, string message) => false;
    }
}
