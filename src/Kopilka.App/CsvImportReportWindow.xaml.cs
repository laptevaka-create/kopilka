using System.Windows;

namespace Kopilka.App;

/// <summary>
/// Окно отчёта импорта CSV: «импортировано X, пропущено Y» и причины
/// по пропущенным строкам. Содержимое — готовый текст, логики нет.
/// </summary>
public partial class CsvImportReportWindow : Window
{
    public CsvImportReportWindow(string report)
    {
        InitializeComponent();
        DataContext = report;
    }
}
