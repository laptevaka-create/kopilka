using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты фильтров журнала (шаг 3): произвольный период через
/// границу года, период из одного дня, повторный ApplyPeriod после ошибки,
/// живучесть фильтра по категории при навигации, сброс фильтра после
/// удаления категории в справочнике и удаление последней строки
/// в отфильтрованном списке.
/// </summary>
public class JournalFilterContractTests
{
    private static (MainViewModel ViewModel, FakeTransactionRepository Transactions, FakeCategoryRepository Categories, FakeDialogService Dialog) Create()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        FakeCategoryRepository categories = Fakes.Categories();
        FakeDialogService dialog = Fakes.Dialog(result: true);
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            categories,
            Fakes.Budgets(),
            dialog,
            Fakes.Clock());
        return (viewModel, transactions, categories, dialog);
    }

    // Сид фейков: счёт 1 «Наличные», 2 «Карта»; категории 1 «Зарплата»
    // (доход), 3 «Продукты», 4 «Транспорт» (расходы). Часы — 15.11.2026.

    [Fact]
    public void ApplyPeriod_AcrossYearBoundary_IncludesTransactionsOfBothYears()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _, _) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 12, 24), null);   // до периода
        transactions.Add(1, 3, 200_00, new DateOnly(2026, 12, 25), null);   // первая дата
        transactions.Add(1, 1, 1_000_00, new DateOnly(2026, 12, 31), null); // доход старого года
        transactions.Add(1, 4, 300_00, new DateOnly(2027, 1, 1), null);     // первый день нового года
        transactions.Add(2, 3, 400_00, new DateOnly(2027, 1, 5), null);     // последняя дата
        transactions.Add(1, 3, 500_00, new DateOnly(2027, 1, 6), null);     // после периода

        // Пробелы вокруг даты — как при вставке из поля ввода.
        viewModel.FilterFromText = " 25.12.2026 ";
        viewModel.FilterToText = "05.01.2027";
        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.True(viewModel.IsCustomPeriod);
        Assert.Equal("Период: 25.12.2026 — 05.01.2027", viewModel.MonthTitle);
        Assert.Equal(4, viewModel.Transactions.Count);
        Assert.Equal(new DateOnly(2026, 12, 25), viewModel.Transactions[0].Date);
        Assert.Equal(new DateOnly(2027, 1, 5), viewModel.Transactions[^1].Date);

        // Итоги считаются по видимым строкам обоих годов.
        Assert.Equal(1_000_00, viewModel.IncomeKopecks);
        Assert.Equal(900_00, viewModel.ExpenseKopecks);
        Assert.Equal(100_00, viewModel.BalanceKopecks);

        // В режиме периода навигация месяцев отключена.
        Assert.False(viewModel.PreviousMonthCommand.CanExecute(null));
        Assert.False(viewModel.NextMonthCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyPeriod_SingleDay_ShowsExactlyThatDay()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _, _) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 9), null);  // до дня
        transactions.Add(1, 3, 200_00, new DateOnly(2026, 11, 10), null); // искомый день
        transactions.Add(1, 4, 300_00, new DateOnly(2026, 11, 10), null); // искомый день
        transactions.Add(1, 3, 400_00, new DateOnly(2026, 11, 11), null); // после дня

        viewModel.FilterFromText = "10.11.2026";
        viewModel.FilterToText = "10.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.True(viewModel.IsCustomPeriod);
        Assert.Equal("Период: 10.11.2026 — 10.11.2026", viewModel.MonthTitle);
        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.All(viewModel.Transactions, row => Assert.Equal(new DateOnly(2026, 11, 10), row.Date));
        Assert.Equal(500_00, viewModel.ExpenseKopecks);
        Assert.Equal(string.Empty, viewModel.FilterError);
    }

    [Fact]
    public void ApplyPeriod_AfterError_SuccessfulRetryClearsFilterError()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _, _) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 10), null);

        viewModel.FilterFromText = "20.11.2026";
        viewModel.FilterToText = "05.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.Equal("Начало периода позже его конца.", viewModel.FilterError);
        Assert.False(viewModel.IsCustomPeriod);

        viewModel.FilterFromText = "05.11.2026";
        viewModel.FilterToText = "20.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        // Успешная попытка — ошибка прошлой попытки исчезает.
        Assert.Equal(string.Empty, viewModel.FilterError);
        Assert.True(viewModel.IsCustomPeriod);
        Assert.Equal("Период: 05.11.2026 — 20.11.2026", viewModel.MonthTitle);
        Assert.Single(viewModel.Transactions);
    }

    [Fact]
    public void FilterByCategory_SurvivesMonthNavigation()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _, _) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 6), null); // Продукты, ноябрь
        transactions.Add(1, 4, 200_00, new DateOnly(2026, 11, 7), null); // Транспорт, ноябрь
        transactions.Add(1, 3, 300_00, new DateOnly(2026, 12, 6), null); // Продукты, декабрь
        transactions.Add(1, 4, 400_00, new DateOnly(2026, 12, 7), null); // Транспорт, декабрь

        viewModel.SelectedFilterCategory = viewModel.FilterCategories.First(c => c.Id == 3);
        Assert.Single(viewModel.Transactions);

        viewModel.NextMonthCommand.Execute(null);

        // Фильтр по категории пережил смену месяца: выбранная
        // категория применяется и к декабрю.
        Assert.Equal("Декабрь 2026", viewModel.MonthTitle);
        Assert.Equal(3L, viewModel.SelectedFilterCategory!.Id);
        TransactionRowViewModel row = Assert.Single(viewModel.Transactions);
        Assert.Equal("Продукты", row.CategoryName);
        Assert.Equal(300_00, row.AmountKopecks);
        Assert.Equal(300_00, viewModel.ExpenseKopecks);
    }

    [Fact]
    public void ShowCategories_WhenFilterCategoryWasDeleted_ResetsFilterToAllCategories()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeCategoryRepository categories, FakeDialogService dialog) = Create();
        transactions.Add(1, 4, 200_00, new DateOnly(2026, 11, 7), null); // Транспорт
        transactions.Add(1, 4, 300_00, new DateOnly(2026, 11, 8), null); // Транспорт
        // Фильтр — на неиспользуемой категории «Развлечения»: только
        // такие категории справочник и позволяет удалить.
        viewModel.SelectedFilterCategory = viewModel.FilterCategories.First(c => c.Id == 5);
        Assert.Empty(viewModel.Transactions);

        // В окне справочника категорию удалили.
        Assert.Equal(DeleteResult.Deleted, categories.Delete(5));
        viewModel.ShowCategoriesCommand.Execute(null);

        Assert.Equal(1, dialog.CategoriesShownCount);
        Assert.DoesNotContain(viewModel.FilterCategories, c => c.Id == 5);
        Assert.Equal("Все категории", viewModel.SelectedFilterCategory!.Display);
        // Фильтр сброшен — снова видны все строки месяца.
        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.Equal(500_00, viewModel.ExpenseKopecks);
    }

    [Fact]
    public void ShowCategories_WhenFilterCategoryRenamed_KeepsSelectionById()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeCategoryRepository categories, _) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 6), null);
        viewModel.SelectedFilterCategory = viewModel.FilterCategories.First(c => c.Id == 3);

        // Категорию переименовали, идентификатор прежний.
        categories.Update(new Category
        {
            Id = 3,
            Name = "Еда",
            Kind = CategoryKind.Expense,
            HasLimit = false,
            SortOrder = 0,
        });
        viewModel.ShowCategoriesCommand.Execute(null);

        // Выбор восстановлен по Id, подпись обновилась до нового имени.
        Assert.Equal(3L, viewModel.SelectedFilterCategory!.Id);
        Assert.Equal("Еда (расход)", viewModel.SelectedFilterCategory.Display);
        TransactionRowViewModel row = Assert.Single(viewModel.Transactions);
        Assert.Equal("Еда", row.CategoryName);
    }

    [Fact]
    public void DeleteTransaction_LastRowInFilteredList_EmptiesListAndTotals()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _, FakeDialogService dialog) = Create();
        transactions.Add(1, 3, 1_000_00, new DateOnly(2026, 11, 6), null);
        viewModel.SelectedFilterCategory = viewModel.FilterCategories.First(c => c.Id == 3);
        viewModel.SelectedTransaction = Assert.Single(viewModel.Transactions);
        dialog.ConfirmResult = true;

        viewModel.DeleteTransactionCommand.Execute(null);

        Assert.Empty(viewModel.Transactions);
        Assert.Empty(transactions.All);
        Assert.Equal(0, viewModel.IncomeKopecks);
        Assert.Equal(0, viewModel.ExpenseKopecks);
        Assert.Equal(0, viewModel.BalanceKopecks);
        Assert.Equal("0,00 ₽", viewModel.IncomeText);
        Assert.Equal("0,00 ₽", viewModel.ExpenseText);
        Assert.Equal("0,00 ₽", viewModel.BalanceText);
        // Удаление — не ошибка фильтра: причина не появляется.
        Assert.Equal(string.Empty, viewModel.FilterError);
        Assert.Null(viewModel.SelectedTransaction);
    }

    [Fact]
    public void EditTransaction_WhenDialogCancelled_KeepsListUnchanged()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _, FakeDialogService dialog) = Create();
        transactions.Add(1, 3, 1_000_00, new DateOnly(2026, 11, 6), null);
        viewModel.Reload();
        viewModel.SelectedTransaction = viewModel.Transactions[0];
        dialog.EditResult = false;

        viewModel.EditTransactionCommand.Execute(null);

        Assert.Equal(1, dialog.EditShownCount);
        TransactionRowViewModel row = Assert.Single(viewModel.Transactions);
        Assert.Equal(1_000_00, row.AmountKopecks);
        Assert.Equal(1_000_00, viewModel.ExpenseKopecks);
    }
}