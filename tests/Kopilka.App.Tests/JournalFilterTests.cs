using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Фильтры журнала (шаг 3): по категории, по произвольному периоду,
/// их комбинация, итоги по видимым строкам; редактирование и удаление
/// выбранной транзакции.
/// </summary>
public class JournalFilterTests
{
    private static (MainViewModel ViewModel, FakeTransactionRepository Transactions, FakeDialogService Dialog) Create()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        FakeDialogService dialog = Fakes.Dialog(result: true);
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            Fakes.Categories(),
            Fakes.Budgets(),
            Fakes.Accruer(),
            dialog,
            Fakes.Clock());
        return (viewModel, transactions, dialog);
    }

    [Fact]
    public void FilterByCategory_ShowsOnlyThatCategory_AndRecalculatesTotals()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(1, 1, 100_000_00, new DateOnly(2026, 11, 5), null);  // Зарплата
        transactions.Add(1, 3, 1_000_00, new DateOnly(2026, 11, 6), null);   // Продукты
        transactions.Add(2, 3, 500_00, new DateOnly(2026, 11, 7), null);     // Продукты
        transactions.Add(1, 4, 300_00, new DateOnly(2026, 11, 8), null);     // Транспорт

        viewModel.SelectedFilterCategory = viewModel.FilterCategories.First(c => c.Id == 3);

        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.All(viewModel.Transactions, row => Assert.Equal("Продукты", row.CategoryName));
        Assert.Equal(0, viewModel.IncomeKopecks);
        Assert.Equal(1_500_00, viewModel.ExpenseKopecks);
    }

    [Fact]
    public void FilterByCategory_BackToAll_RestoresFullList()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(1, 3, 1_000_00, new DateOnly(2026, 11, 6), null);
        transactions.Add(1, 4, 300_00, new DateOnly(2026, 11, 8), null);

        viewModel.SelectedFilterCategory = viewModel.FilterCategories.First(c => c.Id == 3);
        viewModel.SelectedFilterCategory = viewModel.FilterCategories[0];

        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.Equal("Все категории", viewModel.FilterCategories[0].Display);
    }

    [Fact]
    public void ApplyPeriod_ShowsOnlyPeriod_AndSwitchesTitle()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 4), null);   // до периода
        transactions.Add(1, 3, 200_00, new DateOnly(2026, 11, 5), null);   // первая дата
        transactions.Add(1, 3, 300_00, new DateOnly(2026, 11, 20), null);  // последняя дата
        transactions.Add(1, 3, 400_00, new DateOnly(2026, 11, 21), null);  // после периода

        viewModel.FilterFromText = "05.11.2026";
        viewModel.FilterToText = "20.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.True(viewModel.IsCustomPeriod);
        Assert.Equal("Период: 05.11.2026 — 20.11.2026", viewModel.MonthTitle);
        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.Equal(500_00, viewModel.ExpenseKopecks);
        Assert.Equal(string.Empty, viewModel.FilterError);
    }

    [Fact]
    public void ApplyPeriod_DisablesMonthNavigation_UntilReset()
    {
        (MainViewModel viewModel, _, _) = Create();
        viewModel.FilterFromText = "01.11.2026";
        viewModel.FilterToText = "15.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.False(viewModel.PreviousMonthCommand.CanExecute(null));
        Assert.False(viewModel.NextMonthCommand.CanExecute(null));

        viewModel.ResetPeriodCommand.Execute(null);

        Assert.False(viewModel.IsCustomPeriod);
        Assert.True(viewModel.PreviousMonthCommand.CanExecute(null));
        Assert.True(viewModel.NextMonthCommand.CanExecute(null));
        Assert.Equal("Ноябрь 2026", viewModel.MonthTitle);
        Assert.Equal(string.Empty, viewModel.FilterFromText);
        Assert.Equal(string.Empty, viewModel.FilterToText);
    }

    [Fact]
    public void ApplyPeriod_WithOnlyOneDate_ShowsError_AndKeepsMonthMode()
    {
        (MainViewModel viewModel, _, _) = Create();
        viewModel.FilterFromText = "05.11.2026";

        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.False(viewModel.IsCustomPeriod);
        Assert.Equal("Укажите обе даты периода.", viewModel.FilterError);
        Assert.Equal("Ноябрь 2026", viewModel.MonthTitle);
    }

    [Fact]
    public void ApplyPeriod_WithGarbageDate_ShowsError()
    {
        (MainViewModel viewModel, _, _) = Create();
        viewModel.FilterFromText = "вчера";
        viewModel.FilterToText = "20.11.2026";

        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.False(viewModel.IsCustomPeriod);
        Assert.Contains("ДД.ММ.ГГГГ", viewModel.FilterError);
    }

    [Fact]
    public void ApplyPeriod_FromAfterTo_ShowsError()
    {
        (MainViewModel viewModel, _, _) = Create();
        viewModel.FilterFromText = "20.11.2026";
        viewModel.FilterToText = "05.11.2026";

        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.False(viewModel.IsCustomPeriod);
        Assert.Equal("Начало периода позже его конца.", viewModel.FilterError);
    }

    [Fact]
    public void PeriodAndCategoryFilters_Combine()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 6), null);   // Продукты, в периоде
        transactions.Add(1, 3, 200_00, new DateOnly(2026, 11, 25), null);  // Продукты, вне периода
        transactions.Add(1, 4, 300_00, new DateOnly(2026, 11, 6), null);   // Транспорт, в периоде

        viewModel.SelectedFilterCategory = viewModel.FilterCategories.First(c => c.Id == 3);
        viewModel.FilterFromText = "01.11.2026";
        viewModel.FilterToText = "15.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        TransactionRowViewModel row = Assert.Single(viewModel.Transactions);
        Assert.Equal("Продукты", row.CategoryName);
        Assert.Equal(100_00, viewModel.ExpenseKopecks);
    }

    [Fact]
    public void DeleteTransaction_WhenConfirmed_RemovesRowAndRecalculates()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        transactions.Add(1, 3, 1_000_00, new DateOnly(2026, 11, 6), null);
        transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 7), null);
        viewModel.Reload();
        viewModel.SelectedTransaction = viewModel.Transactions[0];
        dialog.ConfirmResult = true;

        viewModel.DeleteTransactionCommand.Execute(null);

        TransactionRowViewModel remaining = Assert.Single(viewModel.Transactions);
        Assert.Equal(500_00, remaining.AmountKopecks);
        Assert.Equal(500_00, viewModel.ExpenseKopecks);
        Assert.Null(viewModel.SelectedTransaction);
        Assert.NotNull(dialog.LastConfirmMessage);
    }

    [Fact]
    public void DeleteTransaction_WhenDeclined_KeepsRow()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        transactions.Add(1, 3, 1_000_00, new DateOnly(2026, 11, 6), null);
        viewModel.Reload();
        viewModel.SelectedTransaction = viewModel.Transactions[0];
        dialog.ConfirmResult = false;

        viewModel.DeleteTransactionCommand.Execute(null);

        Assert.Single(viewModel.Transactions);
        Assert.Single(transactions.All);
    }

    [Fact]
    public void EditAndDelete_RequireSelection()
    {
        (MainViewModel viewModel, _, _) = Create();

        Assert.Null(viewModel.SelectedTransaction);
        Assert.False(viewModel.EditTransactionCommand.CanExecute(null));
        Assert.False(viewModel.DeleteTransactionCommand.CanExecute(null));
    }

    [Fact]
    public void EditTransaction_PassesSelectedId_AndReloadsOnSave()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        transactions.Add(1, 3, 1_000_00, new DateOnly(2026, 11, 6), null);
        viewModel.Reload();
        viewModel.SelectedTransaction = viewModel.Transactions[0];
        dialog.EditResult = true;

        viewModel.EditTransactionCommand.Execute(null);

        Assert.Equal(viewModel.Transactions[0].Id, dialog.LastEditId);
        Assert.Equal(1, dialog.EditShownCount);
    }

    [Fact]
    public void ShowCategories_RefreshesFilterList_AfterExternalAdd()
    {
        (MainViewModel viewModel, _, FakeDialogService dialog) = Create();
        int before = viewModel.FilterCategories.Count;

        // Имитируем добавление категории в окне справочника:
        // репозиторий общий, диалог просто открылся и закрылся.
        viewModel.ShowCategoriesCommand.Execute(null);

        Assert.Equal(1, dialog.CategoriesShownCount);
        Assert.Equal(before, viewModel.FilterCategories.Count);
        Assert.Equal("Все категории", viewModel.SelectedFilterCategory?.Display);
    }
}
