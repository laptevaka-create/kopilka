using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты диалога добавления транзакции: суммы с пробелами
/// внутри числа и с ведущим плюсом, округление к нулю, повторное
/// сохранение после ошибки и двойное успешное сохранение.
/// </summary>
public class TransactionEditContractTests
{
    private static TransactionEditViewModel Create(FakeTransactionRepository transactions) =>
        new(Fakes.Accounts(), Fakes.Categories(), transactions, Fakes.Clock());

    // Сид фейков: счёт 1 «Наличные» (выбран по умолчанию),
    // категория 3 «Продукты» (расход). Часы — 15 ноября 2026.

    [Fact]
    public void Save_AmountWithSpacesInsideDigits_IsAccepted()
    {
        // «1 2 5 0» — пробелы между цифрами трактуются как разделители
        // тысяч (парсер .NET принимает их): значение — 1250 рублей.
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "1 2 5 0";

        bool saved = viewModel.Save();

        Assert.True(saved);
        Assert.Equal(125_000, transactions.All[0].AmountKopecks);
    }

    [Fact]
    public void Save_AmountWithLeadingPlus_IsAccepted()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "+100";

        bool saved = viewModel.Save();

        Assert.True(saved);
        Assert.Equal(10_000, transactions.All[0].AmountKopecks);
    }

    [Fact]
    public void Save_TinyAmount_RoundsToZero_ShowsGreaterThanZeroError()
    {
        // «0,001» рубля — это 0,1 копейки: округляется к нулю,
        // нулевых сумм не бывает — отказ с причиной.
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "0,001";

        bool saved = viewModel.Save();

        Assert.False(saved);
        Assert.Contains("Сумма должна быть больше нуля.", viewModel.ValidationSummary);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public void Save_AfterError_WithCorrectedInput_SavesTransaction()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = viewModel.Categories[2];
        viewModel.AmountText = "абв";

        bool firstAttempt = viewModel.Save();

        Assert.False(firstAttempt);
        Assert.NotEqual(string.Empty, viewModel.ValidationSummary);
        Assert.Empty(transactions.All);

        viewModel.AmountText = "100";
        bool secondAttempt = viewModel.Save();

        Assert.True(secondAttempt);
        Kopilka.Core.Domain.Transaction added = Assert.Single(transactions.All);
        Assert.Equal(10_000, added.AmountKopecks);
    }

    [Fact]
    public void Save_TwiceSuccessfully_AddsTwoTransactions()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        TransactionEditViewModel viewModel = Create(transactions);
        viewModel.SelectedCategory = viewModel.Categories[2];

        viewModel.AmountText = "100";
        Assert.True(viewModel.Save());

        viewModel.AmountText = "200";
        Assert.True(viewModel.Save());

        Assert.Equal(2, transactions.All.Count);
        Assert.Equal(10_000, transactions.All[0].AmountKopecks);
        Assert.Equal(20_000, transactions.All[1].AmountKopecks);
    }
}
