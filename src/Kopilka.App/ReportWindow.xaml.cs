using System.Windows;
using Kopilka.App.Presentation;

namespace Kopilka.App;

/// <summary>
/// Окно отчёта месяца. Вся сборка данных — в ReportViewModel,
/// окно показывает результат.
/// </summary>
public partial class ReportWindow : Window
{
    public ReportWindow(ReportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
