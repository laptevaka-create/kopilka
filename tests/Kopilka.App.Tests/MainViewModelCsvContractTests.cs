using System.Text;
using Kopilka.App.Presentation;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Контрактные тесты команд CSV главной модели на реальных временных
/// файлах: экспорт после навигации по месяцам, импорт файла с BOM,
/// комментарий со «;» в кавычках, пустой файл и недоступный путь
/// экспорта (ошибка окном, без падения).
/// </summary>
public class MainViewModelCsvContractTests : IDisposable
{
    private readonly string _tempDirectory;

    public MainViewModelCsvContractTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "kopilka-csv-contract", Guid.NewGuid().ToString("N"));
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
            // Временная папка системе не мешает.
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
            dialog,
            Fakes.Clock());
        return (viewModel, transactions, dialog);
    }

    private string TempFile(string name) => Path.Combine(_tempDirectory, name);

    [Fact]
    public void ExportCsv_AfterNavigation_ExportsNavigatedMonthOnly()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 20), "ноябрьская");
        transactions.Add(2, 4, 200_00, new DateOnly(2026, 12, 5), "декабрьская");
        transactions.Add(1, 3, 300_00, new DateOnly(2026, 12, 10), null);

        viewModel.NextMonthCommand.Execute(null);
        Assert.Equal("Декабрь 2026", viewModel.MonthTitle);
        string path = TempFile("december.csv");
        dialog.ExportPath = path;

        viewModel.ExportCsvCommand.Execute(null);

        string[] lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Equal("05.12.2026;Карта;Транспорт;200,00;декабрьская", lines[0]);
        Assert.Equal("10.12.2026;Наличные;Продукты;300,00;", lines[1]);
        // Ноябрьских строк в файле нет: экспорт — показанный месяц.
        Assert.DoesNotContain(lines, line => line.Contains("20.11.2026"));
        Assert.Equal(0, dialog.ErrorShownCount);
    }

    [Fact]
    public void ImportCsv_FileWithBom_FirstLineIsValid()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        string path = TempFile("bom.csv");
        File.WriteAllText(
            path,
            "05.12.2026;Наличные;Продукты;100,00;первая\r\n06.12.2026;Карта;Транспорт;50,00;\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        dialog.ImportPath = path;

        viewModel.ImportCsvCommand.Execute(null);

        // BOM съеден: первая строка валидна, обе импортированы.
        Assert.Equal(2, transactions.All.Count);
        Assert.Equal("Импортировано 2, пропущено 0.", dialog.LastImportReport);
        Assert.Contains(transactions.All, t => t.Comment == "первая");
        Assert.Equal(0, dialog.ErrorShownCount);
    }

    [Fact]
    public void ImportCsv_QuotedCommentWithSemicolon_StoredWhole()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        string path = TempFile("quoted.csv");
        File.WriteAllText(path, "05.11.2026;Наличные;Продукты;1250,50;\"хлеб; молоко\"\r\n");
        dialog.ImportPath = path;

        viewModel.ImportCsvCommand.Execute(null);

        Transaction imported = Assert.Single(transactions.All);
        Assert.Equal("хлеб; молоко", imported.Comment);
        // Журнал перечитан: строка видна с целым комментарием.
        TransactionRowViewModel row = Assert.Single(viewModel.Transactions);
        Assert.Equal("хлеб; молоко", row.Comment);
    }

    [Fact]
    public void ImportCsv_EmptyFile_ReportsZeroes()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        string path = TempFile("empty.csv");
        File.WriteAllText(path, string.Empty); // 0 байт
        dialog.ImportPath = path;

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Empty(transactions.All);
        Assert.Equal(1, dialog.ImportReportShownCount);
        Assert.Equal("Импортировано 0, пропущено 0.", dialog.LastImportReport);
        Assert.Equal(0, dialog.ErrorShownCount);
        Assert.Empty(viewModel.Transactions);
    }

    [Fact]
    public void ExportCsv_ToDirectoryPath_ShowsErrorInsteadOfCrash()
    {
        (MainViewModel viewModel, FakeTransactionRepository transactions, FakeDialogService dialog) = Create();
        transactions.Add(1, 3, 100_00, new DateOnly(2026, 11, 5), null);
        // Под Windows запись в путь существующего каталога — отказ
        // доступа: команда не падает, а показывает ошибку.
        dialog.ExportPath = _tempDirectory;

        viewModel.ExportCsvCommand.Execute(null);

        Assert.Equal(1, dialog.ErrorShownCount);
        Assert.Equal("Экспорт CSV", dialog.LastErrorTitle);
        Assert.True(Directory.Exists(_tempDirectory));
    }
}
