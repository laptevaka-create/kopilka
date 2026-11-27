using Kopilka.App.Presentation;
using Kopilka.Core.Data;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контракт сроков регулярных начислений в главной модели: фильтры и
/// сброс периода не начисляют, прочие окна не входят в таблицу сроков,
/// а результаты начисления видны после перезагрузки журнала сразу.
/// </summary>
public class MainViewModelRecurringContractTests
{
    private static (MainViewModel ViewModel, FakeRecurringAccruer Accruer, FakeDialogService Dialog) Create()
    {
        FakeRecurringAccruer accruer = Fakes.Accruer();
        FakeDialogService dialog = Fakes.Dialog(result: false);
        var viewModel = new MainViewModel(
            Fakes.Transactions(),
            Fakes.Accounts(),
            Fakes.Categories(),
            Fakes.Budgets(),
            accruer,
            dialog,
            Fakes.Clock());
        return (viewModel, accruer, dialog);
    }

    [Fact]
    public void ResetPeriod_AfterCustomPeriod_DoesNotTriggerExtraAccrual()
    {
        (MainViewModel viewModel, FakeRecurringAccruer accruer, _) = Create();
        viewModel.FilterFromText = "01.11.2026";
        viewModel.FilterToText = "20.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);
        Assert.True(viewModel.IsCustomPeriod);
        Assert.Equal(1, accruer.Calls);

        viewModel.ResetPeriodCommand.Execute(null);

        Assert.False(viewModel.IsCustomPeriod);
        Assert.Equal("Ноябрь 2026", viewModel.MonthTitle);
        Assert.Equal(1, accruer.Calls);
        Assert.Equal(new DateOnly(2026, 11, 15), accruer.LastToday);
        Assert.Equal(new DateOnly(2026, 11, 15), accruer.LastToDate);
    }

    [Fact]
    public void SelectedFilterCategory_Change_DoesNotTriggerAccrual()
    {
        (MainViewModel viewModel, FakeRecurringAccruer accruer, _) = Create();

        viewModel.SelectedFilterCategory = viewModel.FilterCategories.Single(c => c.Id == 3);
        Assert.Equal(1, accruer.Calls);
        viewModel.SelectedFilterCategory = viewModel.FilterCategories[0];

        Assert.Equal(1, accruer.Calls);
        Assert.Equal(new DateOnly(2026, 11, 15), accruer.LastToday);
        Assert.Equal(new DateOnly(2026, 11, 15), accruer.LastToDate);
    }

    [Fact]
    public void ShowRecurring_InCustomPeriod_UsesRetainedMonthStateForAccrual()
    {
        (MainViewModel viewModel, FakeRecurringAccruer accruer, FakeDialogService dialog) = Create();
        viewModel.FilterFromText = "01.10.2026";
        viewModel.FilterToText = "20.10.2026";
        viewModel.ApplyPeriodCommand.Execute(null);
        Assert.Equal(1, accruer.Calls);

        viewModel.ShowRecurringCommand.Execute(null);

        // Произвольный период не меняет _year/_month: фактически окно
        // шаблонов начисляет до конца сохранённого месяца — ноября 2026.
        Assert.Equal(1, dialog.RecurringShownCount);
        Assert.Equal(2, accruer.Calls);
        Assert.Equal(new DateOnly(2026, 11, 15), accruer.LastToday);
        Assert.Equal(new DateOnly(2026, 11, 30), accruer.LastToDate);
        Assert.True(viewModel.IsCustomPeriod);
    }

    [Fact]
    public void OtherWindowsAndCommands_DoNotTriggerAccrual()
    {
        (MainViewModel viewModel, FakeRecurringAccruer accruer, _) = Create();

        viewModel.AddTransactionCommand.Execute(null);
        viewModel.ShowCategoriesCommand.Execute(null);
        viewModel.ShowBudgetsCommand.Execute(null);
        viewModel.ShowReportCommand.Execute(null);
        viewModel.ExportCsvCommand.Execute(null); // ExportPath null: отмена.
        viewModel.ImportCsvCommand.Execute(null); // ImportPath null: отмена.

        Assert.Equal(1, accruer.Calls);
        Assert.Equal(new DateOnly(2026, 11, 15), accruer.LastToDate);
    }

    [Fact]
    public void Startup_AccruesBeforeInitialReload_AccruedTransactionVisibleImmediately()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        var accruer = new CreatingRecurringAccruer(transactions);

        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            Fakes.Categories(),
            Fakes.Budgets(),
            accruer,
            Fakes.Dialog(result: false),
            Fakes.Clock());

        TransactionRowViewModel row = Assert.Single(viewModel.Transactions);
        Assert.Equal(new DateOnly(2026, 11, 15), row.Date);
        Assert.Equal("начисление", row.Comment);
        Assert.Equal(1, accruer.Calls);
    }

    [Fact]
    public void ShowRecurring_AccruesBeforeReload_NewAccrualVisibleImmediately()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        var accruer = new CreatingRecurringAccruer(transactions);
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            Fakes.Categories(),
            Fakes.Budgets(),
            accruer,
            Fakes.Dialog(result: false),
            Fakes.Clock());
        Assert.Single(viewModel.Transactions);

        viewModel.ShowRecurringCommand.Execute(null);

        Assert.Equal(2, accruer.Calls);
        Assert.Equal(
            new[] { new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 30) },
            viewModel.Transactions.Select(t => t.Date).ToArray());
    }
}

/// <summary>
/// Начислитель, который реально добавляет запись на toDate: позволяет
/// проверить не только число вызовов, но и их порядок относительно Reload.
/// </summary>
internal sealed class CreatingRecurringAccruer : IRecurringAccruer
{
    private readonly FakeTransactionRepository _transactions;

    public CreatingRecurringAccruer(FakeTransactionRepository transactions)
    {
        _transactions = transactions;
    }

    public int Calls { get; private set; }

    public int AccrueUpTo(DateOnly today, DateOnly toDate)
    {
        Calls++;
        _transactions.Add(1, 3, 500_00, toDate, "начисление");
        return 1;
    }
}
