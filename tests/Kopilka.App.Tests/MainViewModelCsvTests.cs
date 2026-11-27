using Kopilka.App.Presentation;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Команды обмена CSV и отчёта (шаг 5): экспорт показанного месяца
/// в файл, импорт с построчной валидацией и перезагрузкой журнала,
/// круглый обмен, отмена диалогов, ошибки чтения/записи, отчёт
/// передаёт показанный месяц и отключён при произвольном периоде.
/// </summary>
public class MainViewModelCsvTests : IDisposable
{
    private readonly string _tempDirectory;

    public MainViewModelCsvTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "kopilka-csv-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Файлы может удерживать система; временная папка ей
            // не мешает.
        }
    }

    private static (MainViewModel ViewModel, FakeTransactionRepository Transactions, FakeDialogService Dialog) Create()
    {
        FakeTransactionRepository transactions = Fakes.Transactions();
        FakeDialogService dialog = Fakes.Dialog(result: true);
        var viewModel = new MainViewModel(
            transactions,
            Fakes.Accounts(),
            Fakes.Categories(),
            Fakes.Budgets(),
            Fakes.Accruer(),
            dialog,
            Fakes.Clock());
        return (viewModel, transactions, dialog);
    }

    private string TempFile(string name) => Path.Combine(_tempDirectory, name);

    [Fact]
    public void ExportCsv_WritesShownMonthOnly()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        transactions.Add(2, 3, 125_050, new DateOnly(2026, 11, 5), "молоко");
        transactions.Add(1, 1, 100_000_00, new DateOnly(2026, 11, 20), null);
        transactions.Add(1, 3, 500_00, new DateOnly(2026, 12, 1), null); // другой месяц
        viewModel.Reload();
        string path = TempFile("export.csv");
        dialog.ExportPath = path;

        viewModel.ExportCsvCommand.Execute(null);

        string[] lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Equal("05.11.2026;Карта;Продукты;1250,50;молоко", lines[0]);
        Assert.Equal("20.11.2026;Наличные;Зарплата;100000,00;", lines[1]);
    }

    [Fact]
    public void ExportCsv_Cancelled_WritesNothing()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 5), null);
        viewModel.Reload();
        dialog.ExportPath = null; // отмена диалога сохранения
        string path = TempFile("cancelled.csv");

        viewModel.ExportCsvCommand.Execute(null);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ExportCsv_DisabledDuringCustomPeriod()
    {
        (MainViewModel viewModel, _, _) = Create();

        viewModel.FilterFromText = "01.11.2026";
        viewModel.FilterToText = "15.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.False(viewModel.ExportCsvCommand.CanExecute(null));
    }

    [Fact]
    public void ImportCsv_ValidAndBrokenLines_AddsValidShowsReport()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        string path = TempFile("import.csv");
        File.WriteAllText(
            path,
            "05.11.2026;Наличные;Продукты;1250,50;молоко\r\n" +
            "05.11.2026;Наличные;Алкоголь;200,00;\r\n" +
            "06.11.2026;Карта;Транспорт;50,00;проезд\r\n");
        dialog.ImportPath = path;

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Equal(2, transactions.All.Count);
        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.Equal(130_050, viewModel.ExpenseKopecks);
        Assert.Equal(1, dialog.ImportReportShownCount);
        Assert.StartsWith("Импортировано 2, пропущено 1.", dialog.LastImportReport);
        Assert.Contains("строка 2: неизвестная категория: «Алкоголь»", dialog.LastImportReport);
    }

    [Fact]
    public void ImportCsv_IntoOtherMonth_ReloadsShownMonthUnchanged()
    {
        // Импорт добавляет записи их собственными датами: журнал
        // показанного месяца перечитывается, записи другого месяца
        // в таблицу не попадают.
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        string path = TempFile("december.csv");
        File.WriteAllText(path, "05.12.2026;Наличные;Продукты;100,00;\r\n");
        dialog.ImportPath = path;

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Single(transactions.All);
        Assert.Empty(viewModel.Transactions);
        Assert.StartsWith("Импортировано 1", dialog.LastImportReport);
    }

    [Fact]
    public void ImportCsv_Cancelled_NothingChanges()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        dialog.ImportPath = null;

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Empty(transactions.All);
        Assert.Equal(0, dialog.ImportReportShownCount);
    }

    [Fact]
    public void ImportCsv_MissingFile_ShowsErrorNotCrash()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        dialog.ImportPath = TempFile("нет-такого-файла.csv");

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Empty(transactions.All);
        Assert.Equal(1, dialog.ErrorShownCount);
        Assert.Equal("Импорт CSV", dialog.LastErrorTitle);
        Assert.Equal(0, dialog.ImportReportShownCount);
    }

    [Fact]
    public void ExportThenImport_RoundTripsTransactions()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        transactions.Add(2, 3, 125_050, new DateOnly(2026, 11, 5), "хлеб; молоко");
        transactions.Add(1, 1, 100_000_00, new DateOnly(2026, 11, 20), null);
        viewModel.Reload();
        string path = TempFile("roundtrip.csv");
        dialog.ExportPath = path;
        viewModel.ExportCsvCommand.Execute(null);

        // Стираем исходные записи и импортируем обратно.
        foreach (Kopilka.Core.Domain.Transaction transaction in transactions.All.ToList())
        {
            transactions.Delete(transaction.Id);
        }
        Assert.Empty(transactions.All);
        dialog.ImportPath = path;

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Equal(2, transactions.All.Count);
        Kopilka.Core.Domain.Transaction products = transactions.All.Single(t => t.CategoryId == 3);
        Assert.Equal(2, products.AccountId);
        Assert.Equal(125_050, products.AmountKopecks);
        Assert.Equal(new DateOnly(2026, 11, 5), products.Date);
        Assert.Equal("хлеб; молоко", products.Comment); // кавычки сняты, «;» на месте
        Kopilka.Core.Domain.Transaction salary = transactions.All.Single(t => t.CategoryId == 1);
        Assert.Null(salary.Comment);
    }

    [Fact]
    public void ShowReport_PassesShownMonth_AndDisabledDuringPeriod()
    {
        (MainViewModel viewModel, _, FakeDialogService dialog) = Create();

        viewModel.ShowReportCommand.Execute(null);

        Assert.Equal(1, dialog.ReportShownCount);
        Assert.Equal("2026-11", dialog.LastReportMonth);

        viewModel.FilterFromText = "01.11.2026";
        viewModel.FilterToText = "15.11.2026";
        viewModel.ApplyPeriodCommand.Execute(null);

        Assert.False(viewModel.ShowReportCommand.CanExecute(null));
    }
}
