using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Начисления регулярных платежей в главной модели (шаг 6): при
/// запуске — до сегодняшнего дня, при открытии месяца — до его конца,
/// после закрытия окна шаблонов — начисление и перезагрузка.
/// </summary>
public class MainViewModelRecurringTests
{
    private static (MainViewModel ViewModel, FakeRecurringAccruer Accruer, FakeDialogService Dialog) Create()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        FakeRecurringAccruer accruer = Fakes.Accruer();
        FakeDialogService dialog = Fakes.Dialog(result: true);
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            Fakes.Categories(),
            Fakes.Budgets(),
            accruer,
            dialog,
            Fakes.Clock());
        return (viewModel, accruer, dialog);
    }

    // Часы — 15.11.2026.

    [Fact]
    public void Startup_AccruesUpToToday()
    {
        (MainViewModel _, FakeRecurringAccruer accruer, _) = Create();

        Assert.Equal(1, accruer.Calls);
        Assert.Equal(new DateOnly(2026, 11, 15), accruer.LastToday);
        Assert.Equal(new DateOnly(2026, 11, 15), accruer.LastToDate);
    }

    [Fact]
    public void NavigationToNextMonth_AccruesToItsEnd()
    {
        (MainViewModel viewModel, FakeRecurringAccruer accruer, _) = Create();

        viewModel.NextMonthCommand.Execute(null);

        Assert.Equal(2, accruer.Calls);
        Assert.Equal(new DateOnly(2026, 11, 15), accruer.LastToday);
        Assert.Equal(new DateOnly(2026, 12, 31), accruer.LastToDate);
    }

    [Fact]
    public void NavigationThroughYearBoundary_AccruesToJanuaryEnd()
    {
        (MainViewModel viewModel, FakeRecurringAccruer accruer, _) = Create();

        viewModel.NextMonthCommand.Execute(null); // декабрь 2026
        viewModel.NextMonthCommand.Execute(null); // январь 2027

        Assert.Equal(3, accruer.Calls);
        Assert.Equal(new DateOnly(2027, 1, 31), accruer.LastToDate);
    }

    [Fact]
    public void NavigationToPreviousMonth_AccruesToItsEnd()
    {
        (MainViewModel viewModel, FakeRecurringAccruer accruer, _) = Create();

        viewModel.PreviousMonthCommand.Execute(null); // октябрь 2026

        Assert.Equal(new DateOnly(2026, 10, 31), accruer.LastToDate);
    }

    [Fact]
    public void ShowRecurring_OpensWindow_AccruesAndReloads()
    {
        (MainViewModel viewModel, FakeRecurringAccruer accruer, FakeDialogService dialog) = Create();

        viewModel.ShowRecurringCommand.Execute(null);

        Assert.Equal(1, dialog.RecurringShownCount);
        Assert.Equal(2, accruer.Calls); // запуск + после закрытия окна
        Assert.Equal(new DateOnly(2026, 11, 30), accruer.LastToDate);
    }

    [Fact]
    public void AccruerAlwaysEnabled_DoesNotDependOnPeriod()
    {
        (MainViewModel viewModel, _, _) = Create();

        viewModel.FilterFromText = "01.11.2026";
        viewModel.FilterToText = "15.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.True(viewModel.ShowRecurringCommand.CanExecute(null));
    }
}
