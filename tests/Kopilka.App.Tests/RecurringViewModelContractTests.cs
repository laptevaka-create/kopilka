using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные края окна шаблонов: нормализация комментария и дня,
/// сохранение полей после добавления, точная подпись активности,
/// сброс редактора и поведение при нескольких ошибках ввода.
/// </summary>
public class RecurringViewModelContractTests
{
    private static (RecurringViewModel ViewModel, FakeRecurringPaymentRepository Payments) Create()
    {
        FakeRecurringPaymentRepository payments = new();
        var viewModel = new RecurringViewModel(Fakes.Categories(), Fakes.Accounts(), payments);
        return (viewModel, payments);
    }

    [Fact]
    public void AddTemplate_WhitespaceComment_StoresNull()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = "5";
        viewModel.AmountText = "100";
        viewModel.CommentText = " \t ";

        viewModel.AddTemplateCommand.Execute(null);

        RecurringPayment stored = Assert.Single(payments.All);
        Assert.Null(stored.Comment);
        Assert.Null(Assert.Single(viewModel.Items).Comment);
    }

    [Fact]
    public void AddTemplate_DayWithSurroundingSpaces_TrimmedAndAccepted()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = " 5 ";
        viewModel.AmountText = "100";

        viewModel.AddTemplateCommand.Execute(null);

        RecurringPayment stored = Assert.Single(payments.All);
        Assert.Equal(5, stored.DayOfMonth);
        Assert.Empty(viewModel.ValidationSummary);
    }

    [Fact]
    public void AddTemplate_FractionalDay_RejectedAsNotInteger()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = "5.0";
        viewModel.AmountText = "100";

        viewModel.AddTemplateCommand.Execute(null);

        Assert.Empty(payments.All);
        Assert.Empty(viewModel.Items);
        Assert.Equal("День месяца — число от 1 до 31.", viewModel.ValidationSummary);
    }

    [Fact]
    public void AddTemplate_KeepsPanelFields_AllowsSecondAddWithSameButton()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.SelectedCategory = viewModel.CategoryOptions.Single(c => c.Id == 4);
        viewModel.SelectedAccount = viewModel.AccountOptions.Single(a => a.Id == 2);
        viewModel.DayText = "5";
        viewModel.AmountText = "25 000";
        viewModel.CommentText = "аренда";
        viewModel.IsActive = false;

        viewModel.AddTemplateCommand.Execute(null);

        // Фактическое поведение: Add перечитывает только список и не
        // очищает/не форматирует редактор. Ввод остаётся как был.
        Assert.Equal("5", viewModel.DayText);
        Assert.Equal("25 000", viewModel.AmountText);
        Assert.Equal("аренда", viewModel.CommentText);
        Assert.Equal(4, viewModel.SelectedCategory!.Id);
        Assert.Equal(2, viewModel.SelectedAccount!.Id);
        Assert.False(viewModel.IsActive);
        Assert.Null(viewModel.SelectedItem);

        viewModel.AddTemplateCommand.Execute(null);

        Assert.Equal(2, payments.All.Count);
        Assert.Equal(2, viewModel.Items.Count);
        Assert.NotEqual(payments.All[0].Id, payments.All[1].Id);
        Assert.All(payments.All, template =>
        {
            Assert.Equal(4, template.CategoryId);
            Assert.Equal(2, template.AccountId);
            Assert.Equal(25_000_00, template.AmountKopecks);
            Assert.Equal(5, template.DayOfMonth);
            Assert.Equal("аренда", template.Comment);
            Assert.False(template.IsActive);
        });
    }

    [Fact]
    public void Display_InactiveTemplate_ExactTextEndsWithOffMarker()
    {
        (RecurringViewModel viewModel, _) = Create();
        viewModel.DayText = "1";
        viewModel.AmountText = "25 000";
        viewModel.IsActive = false;

        viewModel.AddTemplateCommand.Execute(null);

        RecurringViewModel.RecurringItemViewModel item = Assert.Single(viewModel.Items);
        Assert.Equal("Продукты — 25\u00A0000,00 ₽, 1-е число, Наличные — выкл", item.Display);
        Assert.EndsWith(" — выкл", item.Display);
    }

    [Fact]
    public void Display_ActiveTemplate_DoesNotEndWithOffMarker()
    {
        (RecurringViewModel viewModel, _) = Create();
        viewModel.DayText = "5";
        viewModel.AmountText = "100";
        viewModel.IsActive = true;

        viewModel.AddTemplateCommand.Execute(null);

        RecurringViewModel.RecurringItemViewModel item = Assert.Single(viewModel.Items);
        Assert.False(item.Display.EndsWith(" — выкл", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveChanges_WhitespaceComment_StoresNull()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = "5";
        viewModel.AmountText = "100";
        viewModel.CommentText = "исходный";
        viewModel.AddTemplateCommand.Execute(null);
        long id = Assert.Single(payments.All).Id;
        viewModel.SelectedItem = viewModel.Items[0];
        viewModel.CommentText = "   ";

        viewModel.SaveChangesCommand.Execute(null);

        Assert.Null(payments.GetById(id)!.Comment);
        Assert.Empty(viewModel.ValidationSummary);
    }

    [Fact]
    public void SelectedItem_SetToNull_ResetsEditorToDefaults()
    {
        (RecurringViewModel viewModel, _) = Create();
        viewModel.DayText = "5";
        viewModel.AmountText = "100";
        viewModel.CommentText = "комментарий";
        viewModel.IsActive = false;
        viewModel.AddTemplateCommand.Execute(null);
        viewModel.SelectedItem = viewModel.Items[0];
        Assert.Equal("100,00", viewModel.AmountText);

        viewModel.SelectedItem = null;

        Assert.Equal(string.Empty, viewModel.AmountText);
        Assert.Equal(string.Empty, viewModel.DayText);
        Assert.Equal(string.Empty, viewModel.CommentText);
        Assert.True(viewModel.IsActive);
        Assert.Equal(3, viewModel.SelectedCategory!.Id);
        Assert.Equal(1, viewModel.SelectedAccount!.Id);
        Assert.Empty(viewModel.ValidationSummary);
    }

    [Fact]
    public void AddTemplate_MultipleInvalidFields_ListsAllReasonsInInputOrder()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = "5.0";
        viewModel.AmountText = "abc";

        viewModel.AddTemplateCommand.Execute(null);

        Assert.Empty(payments.All);
        Assert.Equal(
            "День месяца — число от 1 до 31." + Environment.NewLine +
            "Сумма — число, например: 1250,50.",
            viewModel.ValidationSummary);
    }

    [Fact]
    public void Constructor_DefaultPicks_ExpenseFirstCategoryAndCashAccount()
    {
        (RecurringViewModel viewModel, _) = Create();

        Assert.Equal(
            new[] { 3L, 4L, 5L, 6L, 1L, 2L },
            viewModel.CategoryOptions.Select(c => c.Id).ToArray());
        Assert.Equal(new[] { 1L, 2L }, viewModel.AccountOptions.Select(a => a.Id).ToArray());
        Assert.Equal(3, viewModel.SelectedCategory!.Id);
        Assert.Equal(1, viewModel.SelectedAccount!.Id);
        Assert.Equal("Продукты (расход)", viewModel.CategoryOptions[0].Display);
    }
}
