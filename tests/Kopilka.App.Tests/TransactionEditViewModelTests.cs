using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

public class TransactionEditViewModelTests
{
    private static TransactionEditViewModel Create(
        FakeTransactionRepository? transactions = null,
        DateTime? clockMoment = null)
    {
        return new TransactionEditViewModel(
            Fakes.Accounts(),
            Fakes.Categories(),
            transactions ?? Fakes.Transactions(),
            clockMoment is null ? Fakes.Clock() : Fakes.Clock(clockMoment.Value));
    }

    [Fact]
    public void Constructor_PrefillsAccountAndDate()
    {
        TransactionEditViewModel viewModel = Create();

        Assert.NotNull(viewModel.SelectedAccount);
        Assert.Equal("Наличные", viewModel.SelectedAccount.Name);
        Assert.Equal(new DateTime(2026, 11, 15), viewModel.SelectedDate);
    }

    [Fact]
    public void Save_ValidInput_AddsTransaction()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedAccount = viewModel.Accounts[1];       // «Карта»
        viewModel.SelectedCategory = viewModel.Categories[2];    // «Продукты», расход
        viewModel.SelectedDate = new DateTime(2026, 11, 6);
        viewModel.AmountText = "1 250,50";
        viewModel.CommentText = "  хлеб и молоко  ";

        bool saved = viewModel.Save();

        Assert.True(saved);
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
        Kopilka.Core.Domain.Transaction added = Assert.Single(transactions.All);
        Assert.Equal(2, added.AccountId);
        Assert.Equal(3, added.CategoryId);
        Assert.Equal(125_050, added.AmountKopecks);
        Assert.Equal(new DateOnly(2026, 11, 6), added.Date);
        Assert.Equal("хлеб и молоко", added.Comment);
    }

    [Fact]
    public void Save_EmptyComment_StoredAsNull()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "100";

        viewModel.Save();

        Assert.Null(transactions.All[0].Comment);
    }

    [Fact]
    public void Save_WhitespaceComment_StoredAsNull()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "100";
        viewModel.CommentText = "   ";

        viewModel.Save();

        Assert.Null(transactions.All[0].Comment);
    }

    [Fact]
    public void Save_WithoutCategory_ShowsErrorAndDoesNotAdd()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = null;
        viewModel.AmountText = "100";

        bool saved = viewModel.Save();

        Assert.False(saved);
        Assert.Contains("Выберите категорию.", viewModel.ValidationSummary);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public void Save_WithoutAccount_ShowsError()
    {
        TransactionEditViewModel viewModel = Create();
        viewModel.SelectedAccount = null;
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "100";

        bool saved = viewModel.Save();

        Assert.False(saved);
        Assert.Contains("Выберите счёт.", viewModel.ValidationSummary);
    }

    [Fact]
    public void Save_WithoutDate_ShowsError()
    {
        TransactionEditViewModel viewModel = Create();
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.SelectedDate = null;
        viewModel.AmountText = "100";

        bool saved = viewModel.Save();

        Assert.False(saved);
        Assert.Contains("Выберите дату.", viewModel.ValidationSummary);
    }

    [Fact]
    public void Save_EmptyAmount_ShowsError()
    {
        TransactionEditViewModel viewModel = Create();
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "  ";

        bool saved = viewModel.Save();

        Assert.False(saved);
        Assert.Contains("Введите сумму.", viewModel.ValidationSummary);
    }

    [Theory]
    [InlineData("абв")]
    [InlineData("12,5,0")]
    [InlineData("1250,50руб")]
    public void Save_NotANumber_ShowsError(string amount)
    {
        TransactionEditViewModel viewModel = Create();
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = amount;

        bool saved = viewModel.Save();

        Assert.False(saved);
        Assert.Contains("Сумма — число, например: 1250,50.", viewModel.ValidationSummary);
    }

    [Theory]
    [InlineData("-100")]
    [InlineData("0")]
    [InlineData("0,00")]
    public void Save_NonPositiveAmount_ShowsError(string amount)
    {
        TransactionEditViewModel viewModel = Create();
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = amount;

        bool saved = viewModel.Save();

        Assert.False(saved);
        Assert.Contains("Сумма должна быть больше нуля.", viewModel.ValidationSummary);
    }

    [Fact]
    public void Save_AfterError_ClearsValidationSummaryOnSuccess()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "абв";
        Assert.False(viewModel.Save());
        Assert.NotEqual(string.Empty, viewModel.ValidationSummary);

        viewModel.AmountText = "100";
        Assert.True(viewModel.Save());

        // Сводка ошибок описывает последнюю попытку: после успеха — пуста.
        Assert.Equal(string.Empty, viewModel.ValidationSummary);
    }

    [Fact]
    public void Save_TooBigAmount_ShowsError_AndDoesNotCrash()
    {
        // Сумма, не помещающаяся в long копеек: отказ с причиной,
        // а не переполнение при приведении типа.
        TransactionEditViewModel viewModel = Create();
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "99999999999999999999999";

        bool saved = viewModel.Save();

        Assert.False(saved);
        Assert.Contains("Сумма слишком большая.", viewModel.ValidationSummary);
    }

    [Theory]
    [InlineData("1250,5", 125050)]
    [InlineData("1250.5", 125050)]     // точка считается опечаткой запятой
    [InlineData("1 250", 125000)]      // пробел-разделитель тысяч
    [InlineData("0,01", 1)]
    public void Save_AcceptsValidAmountFormats(string amount, long expectedKopecks)
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = amount;

        bool saved = viewModel.Save();

        Assert.True(saved);
        Assert.Equal(expectedKopecks, transactions.All[0].AmountKopecks);
    }

    [Fact]
    public void Save_MultipleErrors_AreAllListed()
    {
        TransactionEditViewModel viewModel = Create();
        viewModel.SelectedAccount = null;
        viewModel.SelectedCategory = null;
        viewModel.SelectedDate = null;
        viewModel.AmountText = "";

        viewModel.Save();

        Assert.Contains("Выберите счёт.", viewModel.ValidationSummary);
        Assert.Contains("Выберите категорию.", viewModel.ValidationSummary);
        Assert.Contains("Выберите дату.", viewModel.ValidationSummary);
        Assert.Contains("Введите сумму.", viewModel.ValidationSummary);
    }

    [Fact]
    public void CategoryOptions_ShowKindInLabel()
    {
        TransactionEditViewModel viewModel = Create();

        Assert.Equal("Зарплата (доход)", viewModel.Categories[0].Display);
        Assert.Equal("Продукты (расход)", viewModel.Categories[2].Display);
    }
}
