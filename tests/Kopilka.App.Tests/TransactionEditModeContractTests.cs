using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты режима редактирования транзакции: смена счёта
/// с сохранением идентификатора, повторный Save без дублей и
/// самодостаточность суммы, заполненной BeginEdit.
/// </summary>
public class TransactionEditModeContractTests
{
    private static (TransactionEditViewModel ViewModel, FakeTransactionRepository Transactions) Create()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        var viewModel = new TransactionEditViewModel(
            Fakes.Accounts(), Fakes.Categories(), transactions, Fakes.Clock());
        return (viewModel, transactions);
    }

    [Fact]
    public void Save_AfterBeginEdit_ChangesAccountAndKeepsId()
    {
        (TransactionEditViewModel viewModel, FakeTransactionRepository transactions) = Create();
        long id = transactions.Add(
            accountId: 1, categoryId: 3, amountKopecks: 1_234_56,
            date: new DateOnly(2026, 11, 9), comment: "молоко и хлеб");
        viewModel.BeginEdit(transactions.GetById(id)!);

        // Сумму из BeginEdit («1234,56») не трогаем: повторный Save
        // прогоняет её через парсер и сохраняет те же копейки.
        viewModel.SelectedAccount = viewModel.Accounts.First(a => a.Id == 2); // «Карта»

        Assert.True(viewModel.Save());

        // В репозитории та же запись, а не новая.
        Transaction stored = Assert.Single(transactions.All);
        Assert.Equal(id, stored.Id);
        Assert.Equal(2, stored.AccountId);
        Assert.Equal(3, stored.CategoryId);
        Assert.Equal(1_234_56, stored.AmountKopecks);
        Assert.Equal(new DateOnly(2026, 11, 9), stored.Date);
        Assert.Equal("молоко и хлеб", stored.Comment);
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
    }

    [Fact]
    public void Save_AfterBeginEditTwice_KeepsSingleRecord()
    {
        (TransactionEditViewModel viewModel, FakeTransactionRepository transactions) = Create();
        long id = transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 9), null);
        viewModel.BeginEdit(transactions.GetById(id)!);

        viewModel.AmountText = "750,25";
        Assert.True(viewModel.Save());
        Assert.True(viewModel.Save());

        Transaction stored = Assert.Single(transactions.All);
        Assert.Equal(id, stored.Id);
        Assert.Equal(750_25, stored.AmountKopecks);
    }

    [Fact]
    public void BeginEdit_AfterValidationError_ClearsSummary()
    {
        (TransactionEditViewModel viewModel, FakeTransactionRepository transactions) = Create();
        viewModel.SelectedCategory = viewModel.Categories.First(c => c.Id == 3);
        viewModel.AmountText = "абв";
        Assert.False(viewModel.Save());
        Assert.NotEqual(string.Empty, viewModel.ValidationSummary);

        long id = transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 9), null);
        viewModel.BeginEdit(transactions.GetById(id)!);

        // Переход в режим редактирования — новое состояние диалога,
        // ошибки прошлой попытки сохранения к нему не относятся.
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
        Assert.True(viewModel.IsEditing);
    }
}