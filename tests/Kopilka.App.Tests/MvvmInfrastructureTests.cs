using System.ComponentModel;
using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

public class MvvmInfrastructureTests
{
    private sealed class SampleViewModel : ViewModelBase
    {
        private string? _name;

        public string? Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }

    [Fact]
    public void SetProperty_NewValue_RaisesPropertyChangedWithPropertyName()
    {
        SampleViewModel viewModel = new();
        List<string?> raised = new();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Name = "Копилка";

        Assert.Equal(new[] { "Name" }, raised);
    }

    [Fact]
    public void SetProperty_SameValue_DoesNotRaisePropertyChanged()
    {
        SampleViewModel viewModel = new() { Name = "Копилка" };
        int raised = 0;
        viewModel.PropertyChanged += (_, _) => raised++;

        viewModel.Name = "Копилка";

        Assert.Equal(0, raised);
    }

    [Fact]
    public void RelayCommand_ExecutesAction()
    {
        int executed = 0;
        RelayCommand command = new(() => executed++);

        command.Execute(null);

        Assert.Equal(1, executed);
    }

    [Fact]
    public void RelayCommand_CanExecute_WithoutPredicate_IsAlwaysTrue()
    {
        RelayCommand command = new(() => { });

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void RelayCommand_CanExecute_ReflectsPredicate()
    {
        bool allowed = false;
        RelayCommand command = new(() => { }, () => allowed);

        Assert.False(command.CanExecute(null));
        allowed = true;
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void RelayCommand_RaiseCanExecuteChanged_FiresEvent()
    {
        RelayCommand command = new(() => { });
        int raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void MoneyText_Zero_FormatsAsZero()
    {
        Assert.Equal("0,00 ₽", MoneyText.Format(0));
    }

    [Fact]
    public void MoneyText_Positive_UsesCommaAndThousandGroups()
    {
        // Разделитель групп — неразрывный пробел, как в русской локали.
        Assert.Equal("12\u00A0345,67 ₽", MoneyText.Format(1_234_567));
        Assert.Equal("125,00 ₽", MoneyText.Format(12_500));
    }

    [Fact]
    public void MoneyText_Negative_ShowsMinus()
    {
        Assert.Equal("-45,50 ₽", MoneyText.Format(-4_550));
    }

    [Fact]
    public void MoneyTextSigned_IncomeHasPlus_ExpenseHasMinus()
    {
        Assert.StartsWith("+", MoneyText.FormatSigned(1_000, Kopilka.Core.Domain.CategoryKind.Income));
        Assert.StartsWith("−", MoneyText.FormatSigned(1_000, Kopilka.Core.Domain.CategoryKind.Expense));
    }

    [Fact]
    public void ViewModelBase_ImplementsINotifyPropertyChanged()
    {
        Assert.IsAssignableFrom<INotifyPropertyChanged>(new SampleViewModel());
    }
}
