using System.Windows;
using Kopilka.App.Presentation;

namespace Kopilka.App;

/// <summary>
/// Окно шаблонов регулярных платежей. Вся логика —
/// в RecurringViewModel, окно только показывает и закрывается.
/// </summary>
public partial class RecurringWindow : Window
{
    public RecurringWindow(RecurringViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
