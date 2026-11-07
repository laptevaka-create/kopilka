using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Kopilka.Core.Time;
using Xunit;

namespace Kopilka.App.Tests;

public class MainViewModelTests
{
    private static (MainViewModel ViewModel, FakeTransactionRepository Transactions, FakeDialogService Dialog) Create(
        DateTime? clockMoment = null)
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        FakeDialogService dialog = Fakes.Dialog(result: true);
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            Fakes.Categories(),
            dialog,
            clockMoment is null ? Fakes.Clock() : Fakes.Clock(clockMoment.Value));
        return (viewModel, transactions, dialog);
    }

    [Fact]
    public void Constructor_LoadsCurrentMonth_WithMonthTitle()
    {
        (MainViewModel viewModel, _, _) = Create();

        Assert.Equal("Ноябрь 2026", viewModel.MonthTitle);
        Assert.Empty(viewModel.Transactions);
    }

    [Fact]
    public void Reload_ShowsOnlyTransactionsOfSelectedMonth()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(1, 3, 1_000, new DateOnly(2026, 11, 5), null);
        transactions.Add(1, 3, 2_000, new DateOnly(2026, 10, 31), null);
        transactions.Add(1, 3, 3_000, new DateOnly(2026, 12, 1), null);

        viewModel.Reload();

        Assert.Single(viewModel.Transactions);
        Assert.Equal(new DateOnly(2026, 11, 5), viewModel.Transactions[0].Date);
    }

    [Fact]
    public void Reload_ComputesTotals_FromIncomeAndExpenseCategories()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(1, 1, 120_000_00, new DateOnly(2026, 11, 5), null);  // Зарплата, доход
        transactions.Add(2, 2, 5_000_00, new DateOnly(2026, 11, 6), null);   // Прочие доходы
        transactions.Add(1, 3, 18_450_75, new DateOnly(2026, 11, 7), null);  // Продукты, расход
        transactions.Add(1, 4, 2_500_00, new DateOnly(2026, 11, 8), null);   // Транспорт, расход

        viewModel.Reload();

        Assert.Equal(125_000_00, viewModel.IncomeKopecks);
        Assert.Equal(20_950_75, viewModel.ExpenseKopecks);
        Assert.Equal(104_049_25, viewModel.BalanceKopecks);
        Assert.Equal("125\u00A0000,00 ₽", viewModel.IncomeText);
        Assert.Equal("20\u00A0950,75 ₽", viewModel.ExpenseText);
        Assert.Equal("104\u00A0049,25 ₽", viewModel.BalanceText);
    }

    [Fact]
    public void Rows_ShowAccountAndCategoryNames_WithFormattedAmount()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(2, 1, 1_234_56, new DateOnly(2026, 11, 9), "премия");

        viewModel.Reload();

        TransactionRowViewModel row = Assert.Single(viewModel.Transactions);
        Assert.Equal("Карта", row.AccountName);
        Assert.Equal("Зарплата", row.CategoryName);
        Assert.Equal("премия", row.Comment);
        Assert.Equal("+1\u00A0234,56 ₽", row.DisplayAmount);
        Assert.Equal("09.11.2026", row.DisplayDate);
    }

    [Fact]
    public void Rows_WithoutComment_ShowEmptyText()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 9), null);

        viewModel.Reload();

        Assert.Equal(string.Empty, viewModel.Transactions[0].DisplayComment);
    }

    [Fact]
    public void ExpenseRow_ShowsMinusSign()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 9), null);

        viewModel.Reload();

        Assert.StartsWith("−", viewModel.Transactions[0].DisplayAmount);
    }

    [Fact]
    public void PreviousMonth_MovesToOctober()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create();
        transactions.Add(1, 3, 500_00, new DateOnly(2026, 10, 20), null);

        viewModel.PreviousMonthCommand.Execute(null);

        Assert.Equal("Октябрь 2026", viewModel.MonthTitle);
        Assert.Single(viewModel.Transactions);
    }

    [Fact]
    public void PreviousMonth_FromJanuary_MovesToDecemberOfPreviousYear()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create(clockMoment: new DateTime(2026, 1, 20));
        transactions.Add(1, 3, 500_00, new DateOnly(2025, 12, 31), null);

        viewModel.PreviousMonthCommand.Execute(null);

        Assert.Equal("Декабрь 2025", viewModel.MonthTitle);
        Assert.Single(viewModel.Transactions);
    }

    [Fact]
    public void NextMonth_FromDecember_MovesToJanuaryOfNextYear()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, _) = Create(clockMoment: new DateTime(2026, 12, 20));
        transactions.Add(1, 3, 500_00, new DateOnly(2027, 1, 1), null);

        viewModel.NextMonthCommand.Execute(null);

        Assert.Equal("Январь 2027", viewModel.MonthTitle);
        Assert.Single(viewModel.Transactions);
    }

    [Fact]
    public void AddTransaction_WhenDialogConfirmed_ReloadsList()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        var dialog = new RecordingDialog(transactions);
        var viewModel = new MainViewModel(transactions, Fakes.Accounts(), Fakes.Categories(), dialog, Fakes.Clock());

        viewModel.AddTransactionCommand.Execute(null);

        Assert.Single(viewModel.Transactions);
        Assert.Equal(100_50, viewModel.ExpenseKopecks);
    }

    [Fact]
    public void AddTransaction_WhenDialogCancelled_KeepsListUnchanged()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        transactions.Add(1, 3, 500_00, new DateOnly(2026, 11, 5), null);
        FakeDialogService dialog = Fakes.Dialog(result: false);
        var viewModel = new MainViewModel(transactions, Fakes.Accounts(), Fakes.Categories(), dialog, Fakes.Clock());

        viewModel.AddTransactionCommand.Execute(null);

        Assert.Single(viewModel.Transactions);
        Assert.Equal(500_00, viewModel.ExpenseKopecks);
    }

    /// <summary>Диалог, который при показе добавляет транзакцию — как настоящий с валидной формой.</summary>
    private sealed class RecordingDialog(FakeTransactionRepository transactions) : IDialogService
    {
        public bool ShowAddTransaction()
        {
            transactions.Add(1, 3, 100_50, new DateOnly(2026, 11, 10), "из диалога");
            return true;
        }
    }
}
