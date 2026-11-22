using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты справочника категорий (шаг 3): переименование в то же
/// имя, дубликаты в другом регистре, порядок новых категорий своего типа,
/// отключение команд без выбора и сохранение поверх удалённой категории.
/// </summary>
public class CategoriesViewModelContractTests
{
    private static (CategoriesViewModel ViewModel, FakeCategoryRepository Repository) Create()
    {
        FakeCategoryRepository repository = Fakes.Categories();
        return (new CategoriesViewModel(repository), repository);
    }

    // Сид фейков: расходы — 3 «Продукты» (sort 0), 4 «Транспорт» (1),
    // 5 «Развлечения» (2), 6 «Прочие расходы» (3); доходы —
    // 1 «Зарплата» (0), 2 «Прочие доходы» (1).

    [Fact]
    public void SaveChanges_WithSameName_IsNotAnError()
    {
        (CategoriesViewModel viewModel, FakeCategoryRepository repository) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 3);
        viewModel.EditName = "Продукты"; // имя не менялось — это не дубликат

        viewModel.SaveChangesCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.ValidationSummary);
        Assert.Equal("Продукты", repository.GetById(3)!.Name);
        Assert.Equal(6, viewModel.Categories.Count);
    }

    [Fact]
    public void Add_DuplicateNameInOtherCase_IsRejected()
    {
        (CategoriesViewModel viewModel, FakeCategoryRepository repository) = Create();
        viewModel.NewName = "продукты"; // «Продукты» уже есть в расходах
        viewModel.NewKindIsExpense = true;

        viewModel.AddCategoryCommand.Execute(null);

        Assert.Contains("уже есть", viewModel.ValidationSummary);
        Assert.Equal(6, viewModel.Categories.Count);
        Assert.Equal(6, repository.ListAll().Count);
    }

    [Fact]
    public void SaveChanges_RenameToOtherCategoryNameInOtherCase_IsRejected()
    {
        (CategoriesViewModel viewModel, FakeCategoryRepository repository) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 4); // Транспорт
        viewModel.EditName = "продукты";

        viewModel.SaveChangesCommand.Execute(null);

        Assert.Contains("уже есть", viewModel.ValidationSummary);
        Assert.Equal("Транспорт", repository.GetById(4)!.Name);
    }

    [Fact]
    public void SaveChanges_RenameOwnCategoryToOtherCase_IsAllowed()
    {
        // Смена регистра собственного имени — не дубликат: запись та же.
        (CategoriesViewModel viewModel, FakeCategoryRepository repository) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 3);
        viewModel.EditName = "продукты";

        viewModel.SaveChangesCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.ValidationSummary);
        Assert.Equal("продукты", repository.GetById(3)!.Name);
    }

    [Fact]
    public void Add_NewCategories_GoToEndOfTheirKindBySortOrder()
    {
        (CategoriesViewModel viewModel, FakeCategoryRepository repository) = Create();

        viewModel.NewName = "Кафе";
        viewModel.NewKindIsExpense = true;
        viewModel.AddCategoryCommand.Execute(null);

        // Четыре расходные категории уже есть — новая получает sort_order 4
        // и встаёт последней среди расходов, до доходов.
        Category addedExpense = repository.ListAll().Single(c => c.Name == "Кафе");
        Assert.Equal(4, addedExpense.SortOrder);
        Assert.Equal("Кафе", viewModel.Categories[4].Name);
        Assert.Equal(CategoryKind.Expense, viewModel.Categories[4].Kind);

        viewModel.NewName = "Подработка";
        viewModel.NewKindIsExpense = false;
        viewModel.AddCategoryCommand.Execute(null);

        // Две доходные категории уже есть — sort_order 2, последняя в списке.
        Category addedIncome = repository.ListAll().Single(c => c.Name == "Подработка");
        Assert.Equal(2, addedIncome.SortOrder);
        Assert.Equal("Подработка", viewModel.Categories[^1].Name);
        Assert.Equal(CategoryKind.Income, viewModel.Categories[^1].Kind);
    }

    [Fact]
    public void SelectedCategory_SetToNull_DisablesCommandsAndClearsPanel()
    {
        (CategoriesViewModel viewModel, _) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 3);
        Assert.True(viewModel.SaveChangesCommand.CanExecute(null));
        Assert.True(viewModel.DeleteCategoryCommand.CanExecute(null));

        viewModel.SelectedCategory = null;

        Assert.False(viewModel.SaveChangesCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCategoryCommand.CanExecute(null));
        Assert.Equal(string.Empty, viewModel.EditName);
        Assert.False(viewModel.EditHasLimit);
        Assert.False(viewModel.EditCanHaveLimit);
    }

    [Fact]
    public void SaveChanges_WhenCategoryWasDeletedMeanwhile_ShowsReason()
    {
        (CategoriesViewModel viewModel, FakeCategoryRepository repository) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 3);
        repository.Delete(3); // удалили, пока выбирали новое имя

        viewModel.EditName = "Еда";
        viewModel.SaveChangesCommand.Execute(null);

        Assert.Equal("Категория уже удалена.", viewModel.ValidationSummary);
        Assert.Equal(5, viewModel.Categories.Count);
        Assert.Null(repository.GetById(3));
    }
}