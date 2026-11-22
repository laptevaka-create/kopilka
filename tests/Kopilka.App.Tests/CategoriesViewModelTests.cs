using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Справочник категорий (шаг 3): добавление с проверками, переименование,
/// признак лимита, удаление с защитой используемых категорий.
/// </summary>
public class CategoriesViewModelTests
{
    private static (CategoriesViewModel ViewModel, FakeCategoryRepository Repository) Create()
    {
        FakeCategoryRepository repository = Fakes.Categories();
        return (new CategoriesViewModel(repository), repository);
    }

    [Fact]
    public void Constructor_LoadsCategories_ExpensesFirst()
    {
        (CategoriesViewModel viewModel, _) = Create();

        Assert.Equal(6, viewModel.Categories.Count);
        Assert.Equal(CategoryKind.Expense, viewModel.Categories[0].Kind);
        Assert.Equal(CategoryKind.Income, viewModel.Categories[^1].Kind);
    }

    [Fact]
    public void Add_ValidExpense_AppearsInListWithLimitFlag()
    {
        (CategoriesViewModel viewModel, _) = Create();
        viewModel.NewName = "Кафе";
        viewModel.NewKindIsExpense = true;
        viewModel.NewHasLimit = true;

        viewModel.AddCategoryCommand.Execute(null);

        var added = Assert.Single(viewModel.Categories, c => c.Name == "Кафе");
        Assert.Equal(CategoryKind.Expense, added.Kind);
        Assert.True(added.HasLimit);
        Assert.Equal("Кафе (расход, лимит)", added.Display);
        Assert.Equal(string.Empty, viewModel.NewName);
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
    }

    [Fact]
    public void Add_EmptyName_ShowsError_AndDoesNotAdd()
    {
        (CategoriesViewModel viewModel, _) = Create();
        viewModel.NewName = "   ";

        viewModel.AddCategoryCommand.Execute(null);

        Assert.Equal(6, viewModel.Categories.Count);
        Assert.Equal("Введите имя категории.", viewModel.ValidationSummary);
    }

    [Fact]
    public void Add_DuplicateNameInSameKind_ShowsError()
    {
        (CategoriesViewModel viewModel, _) = Create();
        viewModel.NewName = "Продукты";
        viewModel.NewKindIsExpense = true;

        viewModel.AddCategoryCommand.Execute(null);

        Assert.Equal(6, viewModel.Categories.Count);
        Assert.Contains("уже есть", viewModel.ValidationSummary);
    }

    [Fact]
    public void Add_SameNameInOtherKind_Allowed()
    {
        // Уникальность — пара (тип, имя): «Прочие» может быть и доходом,
        // и расходом, это штатный случай справочника.
        (CategoriesViewModel viewModel, _) = Create();
        viewModel.NewName = "Продукты";
        viewModel.NewKindIsExpense = false; // доход с именем расходной категории

        viewModel.AddCategoryCommand.Execute(null);

        Assert.Equal(7, viewModel.Categories.Count);
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
    }

    [Fact]
    public void Add_IncomeCategory_LimitIsForcedOff()
    {
        (CategoriesViewModel viewModel, _) = Create();
        viewModel.NewName = "Подработка";
        viewModel.NewHasLimit = true;      // включили, пока тип был «расход»
        viewModel.NewKindIsExpense = false; // переключили на доход

        viewModel.AddCategoryCommand.Execute(null);

        var added = Assert.Single(viewModel.Categories, c => c.Name == "Подработка");
        Assert.False(added.HasLimit);
    }

    [Fact]
    public void SelectingCategory_FillsEditPanel()
    {
        (CategoriesViewModel viewModel, _) = Create();

        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 3);

        Assert.Equal("Продукты", viewModel.EditName);
        Assert.True(viewModel.EditCanHaveLimit);
        Assert.True(viewModel.SaveChangesCommand.CanExecute(null));
        Assert.True(viewModel.DeleteCategoryCommand.CanExecute(null));
    }

    [Fact]
    public void SelectingIncomeCategory_DisablesLimitEdit()
    {
        (CategoriesViewModel viewModel, _) = Create();

        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 1); // Зарплата

        Assert.False(viewModel.EditCanHaveLimit);
    }

    [Fact]
    public void SaveChanges_RenamesCategory_AndKeepsSelection()
    {
        (CategoriesViewModel viewModel, FakeCategoryRepository repository) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 3);
        viewModel.EditName = "Еда";

        viewModel.SaveChangesCommand.Execute(null);

        Assert.Equal("Еда", repository.GetById(3)!.Name);
        Assert.Equal("Еда", viewModel.SelectedCategory?.Name);
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
    }

    [Fact]
    public void SaveChanges_DuplicateName_ShowsError()
    {
        (CategoriesViewModel viewModel, _) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 3);
        viewModel.EditName = "Транспорт"; // уже есть в расходах

        viewModel.SaveChangesCommand.Execute(null);

        Assert.Contains("уже есть", viewModel.ValidationSummary);
        Assert.Equal("Продукты", viewModel.Categories.First(c => c.Id == 3).Name);
    }

    [Fact]
    public void Delete_UsedCategory_ShowsReason_AndKeepsIt()
    {
        (CategoriesViewModel viewModel, FakeCategoryRepository repository) = Create();
        repository.InUseIds.Add(3);
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 3);

        viewModel.DeleteCategoryCommand.Execute(null);

        Assert.NotNull(repository.GetById(3));
        Assert.Contains("используется", viewModel.ValidationSummary);
        Assert.Equal(6, viewModel.Categories.Count);
    }

    [Fact]
    public void Delete_UnusedCategory_RemovesFromList()
    {
        (CategoriesViewModel viewModel, FakeCategoryRepository repository) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 5);

        viewModel.DeleteCategoryCommand.Execute(null);

        Assert.Null(repository.GetById(5));
        Assert.Equal(5, viewModel.Categories.Count);
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
    }

    [Fact]
    public void Delete_UnusedCategory_ClearsSelectionAndPanel()
    {
        // Модель не может полагаться на отклик списка (ListBox снимает
        // выделение при Clear): контракт VM самодостаточен — после
        // удаления панель пуста и команды выключены.
        (CategoriesViewModel viewModel, _) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 5);
        viewModel.EditName = "Что-то";

        viewModel.DeleteCategoryCommand.Execute(null);

        Assert.Null(viewModel.SelectedCategory);
        Assert.Equal(string.Empty, viewModel.EditName);
        Assert.False(viewModel.EditHasLimit);
        Assert.False(viewModel.SaveChangesCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCategoryCommand.CanExecute(null));
    }
}
