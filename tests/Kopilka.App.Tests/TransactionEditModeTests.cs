using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Режим редактирования диалога транзакции (шаг 3): поля заполняются
/// существующей транзакцией, сохранение обновляет запись, а не создаёт
/// новую; конфликт с удалённой записью объясняется пользователю.
/// </summary>
public class TransactionEditModeTests
{
    private static (TransactionEditViewModel ViewModel, FakeTransactionRepository Transactions) Create()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        var viewModel = new TransactionEditViewModel(
            Fakes.Accounts(), Fakes.Categories(), transactions, Fakes.Clock());
        return (viewModel, transactions);
    }

    [Fact]
    public void AddMode_IsDefault()
    {
        (TransactionEditViewModel viewModel, _) = Create();

        Assert.False(viewModel.IsEditing);
        Assert.Equal("Добавить транзакцию", viewModel.Title);
    }

    [Fact]
    public void BeginEdit_FillsFieldsFromTransaction()
    {
        (TransactionEditViewModel viewModel, FakeTransactionRepository transactions) = Create();
        long id = transactions.Add(
            accountId: 2, categoryId: 3, amountKopecks: 1_234_56,
            date: new DateOnly(2026, 11, 9), comment: "молоко и хлеб");

        viewModel.BeginEdit(transactions.GetById(id)!);

        Assert.True(viewModel.IsEditing);
        Assert.Equal("Изменить транзакцию", viewModel.Title);
        Assert.Equal(2, viewModel.SelectedAccount!.Id);
        Assert.Equal(3, viewModel.SelectedCategory!.Id);
        Assert.Equal(new DateTime(2026, 11, 9), viewModel.SelectedDate);
        Assert.Equal("1234,56", viewModel.AmountText);
        Assert.Equal("молоко и хлеб", viewModel.CommentText);
    }

    [Fact]
    public void BeginEdit_NullComment_BecomesEmptyText()
    {
        (TransactionEditViewModel viewModel, FakeTransactionRepository transactions) = Create();
        long id = transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 9), null);

        viewModel.BeginEdit(transactions.GetById(id)!);

        Assert.Equal(string.Empty, viewModel.CommentText);
    }

    [Fact]
    public void Save_InEditMode_UpdatesExisting_AndDoesNotAddNew()
    {
        (TransactionEditViewModel viewModel, FakeTransactionRepository transactions) = Create();
        long id = transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 9), null);
        viewModel.BeginEdit(transactions.GetById(id)!);

        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 4);
        viewModel.AmountText = "750,25";
        viewModel.CommentText = "исправлено";

        bool saved = viewModel.Save();

        Assert.True(saved);
        Transaction transaction = Assert.Single(transactions.All);
        Assert.Equal(id, transaction.Id);
        Assert.Equal(4, transaction.CategoryId);
        Assert.Equal(750_25, transaction.AmountKopecks);
        Assert.Equal("исправлено", transaction.Comment);
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
    }

    [Fact]
    public void Save_InEditMode_CanMoveTransactionToAnotherMonth()
    {
        (TransactionEditViewModel viewModel, FakeTransactionRepository transactions) = Create();
        long id = transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 9), null);
        viewModel.BeginEdit(transactions.GetById(id)!);

        viewModel.SelectedDate = new DateTime(2026, 10, 31);

        Assert.True(viewModel.Save());
        Assert.Equal(new DateOnly(2026, 10, 31), transactions.GetById(id)!.Date);
    }

    [Fact]
    public void Save_InEditMode_WhenTransactionDeleted_ShowsReason()
    {
        (TransactionEditViewModel viewModel, FakeTransactionRepository transactions) = Create();
        long id = transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 9), null);
        viewModel.BeginEdit(transactions.GetById(id)!);

        transactions.Delete(id); // удалили, пока диалог открыт

        bool saved = viewModel.Save();

        Assert.False(saved);
        Assert.Equal("Транзакция уже удалена.", viewModel.ValidationSummary);
    }
}
