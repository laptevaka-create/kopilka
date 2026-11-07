using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты каркаса MVVM: форматирование сумм на границах
/// (копейки, триллионы, максимум long), точный текст FormatSigned,
/// команда с параметром, реакция CanExecute на изменение условия
/// и уведомления нескольких свойств ViewModelBase.
/// </summary>
public class MvvmInfrastructureContractTests
{
    [Fact]
    public void MoneyText_OneKopeck_FormatsAsOneKopeck()
    {
        Assert.Equal("0,01 ₽", MoneyText.Format(1));
    }

    [Fact]
    public void MoneyText_99Kopecks_FormatsUnderOneRuble()
    {
        Assert.Equal("0,99 ₽", MoneyText.Format(99));
    }

    [Fact]
    public void MoneyText_TrillionValue_GroupsEveryThreeDigits()
    {
        // Полная группировка разрядов неразрывными пробелами,
        // копейки при этом не теряются.
        Assert.Equal("1\u00A0234\u00A0567\u00A0890\u00A0123,45 ₽", MoneyText.Format(123_456_789_012_345));
    }

    [Fact]
    public void MoneyText_LongMaxValue_DoesNotOverflow()
    {
        // Граница типа: максимальные копейки форматируются без
        // переполнения и потери точности.
        Assert.Equal("92\u00A0233\u00A0720\u00A0368\u00A0547\u00A0758,07 ₽", MoneyText.Format(long.MaxValue));
    }

    [Fact]
    public void MoneyText_FormatSigned_ExactTextForBothKinds()
    {
        Assert.Equal("+125,00 ₽", MoneyText.FormatSigned(12_500, CategoryKind.Income));
        // Знак расхода — типографский минус U+2212, не дефис.
        Assert.Equal("\u2212125,00 ₽", MoneyText.FormatSigned(12_500, CategoryKind.Expense));
    }

    [Fact]
    public void RelayCommand_WithParameter_PassesParameterToExecuteAndCanExecute()
    {
        object? received = null;
        var command = new RelayCommand(
            parameter => { received = parameter; },
            parameter => parameter is int value && value > 0);

        Assert.True(command.CanExecute(5));
        Assert.False(command.CanExecute(-1));
        Assert.False(command.CanExecute("не число"));

        command.Execute(5);

        int receivedValue = Assert.IsType<int>(received);
        Assert.Equal(5, receivedValue);
    }

    [Fact]
    public void RelayCommand_CanExecuteChanged_WhenConditionChanges_SubscribersRequery()
    {
        bool allowed = false;
        var command = new RelayCommand(() => { }, () => allowed);
        var observed = new List<bool>();
        command.CanExecuteChanged += (_, _) => observed.Add(command.CanExecute(null));

        Assert.False(command.CanExecute(null));
        allowed = true;

        command.RaiseCanExecuteChanged();

        // Условие изменилось и событие поднято: подписчик, перечитав
        // CanExecute, видит новое состояние.
        Assert.True(command.CanExecute(null));
        bool reloaded = Assert.Single(observed);
        Assert.True(reloaded);
    }

    [Fact]
    public void ViewModelBase_NotifiesEachOfSeveralPropertiesSeparately()
    {
        SampleViewModel viewModel = new();
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Name = "Копилка";
        viewModel.Amount = 100;
        viewModel.Name = "Сберкнижка";

        // Каждое свойство уведомляется своим именем; установка одного
        // не поднимает уведомление для другого.
        Assert.Equal(new[] { "Name", "Amount", "Name" }, raised);
    }

    private sealed class SampleViewModel : ViewModelBase
    {
        private string? _name;
        private int _amount;

        public string? Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Amount
        {
            get => _amount;
            set => SetProperty(ref _amount, value);
        }
    }
}
