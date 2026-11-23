using System.Windows;
using Kopilka.App.Presentation;

namespace Kopilka.App;

/// <summary>
/// Окно бюджетов месяца. Вся логика — в BudgetsViewModel,
/// окно только показывает и закрывается.
/// </summary>
public partial class BudgetsWindow : Window
{
    public BudgetsWindow(BudgetsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
