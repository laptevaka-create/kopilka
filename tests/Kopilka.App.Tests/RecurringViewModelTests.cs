using Kopilka.App.Presentation;
using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Xunit;

namespace Kopilka.App.Tests;

/// <summary>
/// Окно шаблонов регулярных платежей (шаг 6): добавление с валидацией,
/// правка выбранного, удаление, признак активности, подпись списка.
/// </summary>
public class RecurringViewModelTests
{
    private static (RecurringViewModel ViewModel, FakeRecurringPaymentRepository Payments) Create()
    {
        FakeRecurringPaymentRepository payments = new();
        var viewModel = new RecurringViewModel(Fakes.Categories(), Fakes.Accounts(), payments);
        return (viewModel, payments);
    }

    [Fact]
    public void Constructor_EmptyList_OptionsPreselected()
    {
        (RecurringViewModel viewModel, _) = Create();

        Assert.Empty(viewModel.Items);
        Assert.NotNull(viewModel.SelectedCategory);
        Assert.NotNull(viewModel.SelectedAccount);
        Assert.True(viewModel.IsActive);
        Assert.True(viewModel.AddTemplateCommand.CanExecute(null));
    }

    [Fact]
    public void Add_Valid_AppearsInList()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = "5";
        viewModel.AmountText = "25 000";
        viewModel.CommentText = "  аренда  ";

        viewModel.AddTemplateCommand.Execute(null);

        RecurringViewModel.RecurringItemViewModel item = Assert.Single(viewModel.Items);
        Assert.Equal(25_000_00, item.AmountKopecks);
        Assert.Equal(5, item.DayOfMonth);
        Assert.Equal("аренда", item.Comment);
        Assert.True(item.IsActive);
        Assert.Equal("Продукты — 25\u00A0000,00 ₽, 5-е число, Наличные", item.Display);
        Assert.Empty(viewModel.ValidationSummary);
        Assert.Single(payments.All);
    }

    [Fact]
    public void Add_InvalidDay_ShowsReason()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.AmountText = "100";

        foreach (string badDay in new[] { "0", "32", "abc", "" })
        {
            viewModel.DayText = badDay;
            viewModel.AddTemplateCommand.Execute(null);
            Assert.Contains("День месяца", viewModel.ValidationSummary);
        }

        Assert.Empty(payments.All);
    }

    [Fact]
    public void Add_InvalidAmount_ShowsReason()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = "5";

        viewModel.AmountText = "много";
        viewModel.AddTemplateCommand.Execute(null);
        Assert.Contains("Сумма — число", viewModel.ValidationSummary);

        viewModel.AmountText = "0";
        viewModel.AddTemplateCommand.Execute(null);
        Assert.Contains("Сумма должна быть больше нуля.", viewModel.ValidationSummary);

        Assert.Empty(payments.All);
    }

    [Fact]
    public void SelectingTemplate_FillsEditor_SavingUpdates()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = "5";
        viewModel.AmountText = "25000";
        viewModel.AddTemplateCommand.Execute(null);
        long id = Assert.Single(payments.All).Id;

        // Выбор заполняет панель значениями шаблона, правим и сохраняем.
        viewModel.SelectedItem = viewModel.Items[0];
        Assert.Equal("25000,00", viewModel.AmountText);
        viewModel.AmountText = "27000";
        viewModel.DayText = "10";
        viewModel.CommentText = "переехали";
        viewModel.SaveChangesCommand.Execute(null);

        RecurringPayment stored = payments.GetById(id)!;
        Assert.Equal(27_000_00, stored.AmountKopecks);
        Assert.Equal(10, stored.DayOfMonth);
        Assert.Equal("переехали", stored.Comment);
        Assert.Equal("Продукты — 27\u00A0000,00 ₽, 10-е число, Наличные", viewModel.SelectedItem!.Display);
    }

    [Fact]
    public void InactiveTemplate_MarkedInList_AndSavedAsInactive()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = "1";
        viewModel.AmountText = "100";
        viewModel.IsActive = false;

        viewModel.AddTemplateCommand.Execute(null);

        RecurringViewModel.RecurringItemViewModel item = Assert.Single(viewModel.Items);
        Assert.False(item.IsActive);
        Assert.EndsWith(" — выкл", item.Display);
        Assert.False(payments.GetById(item.Id)!.IsActive);
    }

    [Fact]
    public void Delete_RemovesTemplate_AndClearsSelection()
    {
        (RecurringViewModel viewModel, FakeRecurringPaymentRepository payments) = Create();
        viewModel.DayText = "5";
        viewModel.AmountText = "100";
        viewModel.AddTemplateCommand.Execute(null);
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.DeleteTemplateCommand.Execute(null);

        Assert.Empty(viewModel.Items);
        Assert.Null(viewModel.SelectedItem);
        Assert.False(viewModel.SaveChangesCommand.CanExecute(null));
        Assert.False(viewModel.DeleteTemplateCommand.CanExecute(null));
    }
}

/// <summary>Фейковый репозиторий шаблонов для этого файла.</summary>
internal sealed class FakeRecurringPaymentRepository : IRecurringPaymentRepository
{
    private readonly List<RecurringPayment> _templates = new();
    private long _nextId = 1;

    public IReadOnlyList<RecurringPayment> All => _templates;

    public IReadOnlyList<RecurringPayment> ListAll() =>
        _templates.OrderBy(r => r.DayOfMonth).ThenBy(r => r.Id).ToList();

    public RecurringPayment? GetById(long id) => _templates.FirstOrDefault(r => r.Id == id);

    public long Add(long categoryId, long accountId, long amountKopecks, int dayOfMonth, string? comment, bool isActive)
    {
        var template = new RecurringPayment
        {
            Id = _nextId++,
            CategoryId = categoryId,
            AccountId = accountId,
            AmountKopecks = amountKopecks,
            DayOfMonth = dayOfMonth,
            Comment = comment,
            IsActive = isActive,
        };
        _templates.Add(template);
        return template.Id;
    }

    public bool Update(RecurringPayment template)
    {
        int index = _templates.FindIndex(r => r.Id == template.Id);
        if (index < 0)
        {
            return false;
        }

        _templates[index] = template;
        return true;
    }

    public bool Delete(long id) => _templates.RemoveAll(r => r.Id == id) > 0;
}
