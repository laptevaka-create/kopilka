using System.Collections.ObjectModel;
using Kopilka.Core.Data;
using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kopilka.App.Presentation;

/// <summary>
/// Модель представления справочника категорий: список, добавление,
/// переименование и признак лимита, удаление с защитой истории.
/// Тип категории задаётся при создании и дальше не меняется: смена типа
/// переврала бы знак уже записанных транзакций.
/// Не знает о View — тестируется без интерфейса.
/// </summary>
public sealed class CategoriesViewModel : ViewModelBase
{
    private readonly ICategoryRepository _categories;

    private CategoryItemViewModel? _selectedCategory;
    private string _editName = string.Empty;
    private bool _editHasLimit;
    private string _newName = string.Empty;
    private bool _newKindIsExpense = true;
    private bool _newHasLimit;
    private string _validationSummary = string.Empty;

    public CategoriesViewModel(ICategoryRepository categories)
    {
        _categories = categories;

        Categories = new ObservableCollection<CategoryItemViewModel>();
        AddCategoryCommand = new RelayCommand(AddCategory);
        SaveChangesCommand = new RelayCommand(SaveChanges, () => SelectedCategory is not null);
        DeleteCategoryCommand = new RelayCommand(DeleteCategory, () => SelectedCategory is not null);

        Reload();
    }

    /// <summary>Категории: сначала расходы, затем доходы (порядок репозитория).</summary>
    public ObservableCollection<CategoryItemViewModel> Categories { get; }

    /// <summary>Выбранная в списке категория; заполняет панель редактирования.</summary>
    public CategoryItemViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                EditName = value?.Name ?? string.Empty;
                EditHasLimit = value?.HasLimit ?? false;
                SaveChangesCommand.RaiseCanExecuteChanged();
                DeleteCategoryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Имя выбранной категории в панели редактирования.</summary>
    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    /// <summary>Признак лимита выбранной категории.</summary>
    public bool EditHasLimit
    {
        get => _editHasLimit;
        set => SetProperty(ref _editHasLimit, value);
    }

    /// <summary>Лимит имеет смысл только у категории расхода.</summary>
    public bool EditCanHaveLimit => SelectedCategory?.Kind == CategoryKind.Expense;

    /// <summary>Имя новой категории.</summary>
    public string NewName
    {
        get => _newName;
        set => SetProperty(ref _newName, value);
    }

    /// <summary>Тип новой категории: true — расход, false — доход.</summary>
    public bool NewKindIsExpense
    {
        get => _newKindIsExpense;
        set
        {
            if (SetProperty(ref _newKindIsExpense, value))
            {
                if (!value)
                {
                    NewHasLimit = false;
                }

                RaisePropertyChanged(nameof(NewKindIsIncome));
                RaisePropertyChanged(nameof(NewCanSetLimit));
            }
        }
    }

    /// <summary>Зеркало NewKindIsExpense для второй радиокнопки.</summary>
    public bool NewKindIsIncome
    {
        get => !NewKindIsExpense;
        set => NewKindIsExpense = !value;
    }

    /// <summary>Признак лимита новой категории.</summary>
    public bool NewHasLimit
    {
        get => _newHasLimit;
        set => SetProperty(ref _newHasLimit, value);
    }

    /// <summary>Флажок лимита доступен только для категории расхода.</summary>
    public bool NewCanSetLimit => NewKindIsExpense;

    /// <summary>Ошибки последней операции, построчно.</summary>
    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    public RelayCommand AddCategoryCommand { get; }

    public RelayCommand SaveChangesCommand { get; }

    public RelayCommand DeleteCategoryCommand { get; }

    /// <summary>Перечитывает список категорий из репозитория.</summary>
    public void Reload()
    {
        Categories.Clear();
        foreach (Category category in _categories.ListAll())
        {
            Categories.Add(new CategoryItemViewModel(
                category.Id, category.Name, category.Kind, category.HasLimit, category.SortOrder));
        }
    }

    private void AddCategory()
    {
        string name = NewName.Trim();
        CategoryKind kind = NewKindIsExpense ? CategoryKind.Expense : CategoryKind.Income;

        if (name.Length == 0)
        {
            ValidationSummary = "Введите имя категории.";
            return;
        }

        if (NameExists(name, kind, excludeId: null))
        {
            ValidationSummary = $"Категория «{name}» уже есть в этом типе.";
            return;
        }

        // Лимит имеет смысл только у расходов; у дохода он всегда снят.
        bool hasLimit = NewKindIsExpense && NewHasLimit;
        int sortOrder = _categories.ListAll().Count(c => c.Kind == kind);

        try
        {
            _categories.Add(name, kind, hasLimit, sortOrder);
        }
        catch (DbUpdateException)
        {
            // Страховка на случай, если база изменилась между проверкой
            // и записью: уникальность (тип, имя) держит и индекс.
            ValidationSummary = $"Категория «{name}» уже есть в этом типе.";
            return;
        }

        NewName = string.Empty;
        NewHasLimit = false;
        ValidationSummary = string.Empty;
        Reload();
    }

    private void SaveChanges()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        string name = EditName.Trim();
        if (name.Length == 0)
        {
            ValidationSummary = "Имя категории не может быть пустым.";
            return;
        }

        if (NameExists(name, SelectedCategory.Kind, excludeId: SelectedCategory.Id))
        {
            ValidationSummary = $"Категория «{name}» уже есть в этом типе.";
            return;
        }

        bool hasLimit = SelectedCategory.Kind == CategoryKind.Expense && EditHasLimit;
        try
        {
            bool updated = _categories.Update(new Category
            {
                Id = SelectedCategory.Id,
                Name = name,
                Kind = SelectedCategory.Kind,
                HasLimit = hasLimit,
                SortOrder = SelectedCategory.SortOrder,
            });
            if (!updated)
            {
                ValidationSummary = "Категория уже удалена.";
                Reload();
                return;
            }
        }
        catch (DbUpdateException)
        {
            ValidationSummary = $"Категория «{name}» уже есть в этом типе.";
            return;
        }

        ValidationSummary = string.Empty;
        long selectedId = SelectedCategory.Id;
        Reload();
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == selectedId);
    }

    private void DeleteCategory()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        DeleteResult result = _categories.Delete(SelectedCategory.Id);
        switch (result)
        {
            case DeleteResult.Deleted:
                ValidationSummary = string.Empty;
                Reload();
                // Выбор сбрасывается самой моделью, а не откликом списка:
                // иначе без View панель продолжает показывать удалённую
                // категорию, а команды остаются включёнными.
                SelectedCategory = null;
                break;
            case DeleteResult.InUse:
                ValidationSummary =
                    "Категория используется в транзакциях, бюджетах или регулярных платежах. " +
                    "Удаление запрещено, чтобы не потерять историю.";
                break;
            default:
                ValidationSummary = "Категория уже удалена.";
                Reload();
                SelectedCategory = null;
                break;
        }
    }

    private bool NameExists(string name, CategoryKind kind, long? excludeId) =>
        _categories.ListAll().Any(c =>
            c.Kind == kind
            && c.Id != excludeId
            && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Строка списка категорий.</summary>
    public sealed record CategoryItemViewModel(long Id, string Name, CategoryKind Kind, bool HasLimit, int SortOrder)
    {
        /// <summary>Тип словом: «доход» или «расход».</summary>
        public string KindDisplay => Kind == CategoryKind.Income ? "доход" : "расход";

        /// <summary>Подпись в списке: «Продукты (расход, лимит)».</summary>
        public string Display =>
            HasLimit ? $"{Name} ({KindDisplay}, лимит)" : $"{Name} ({KindDisplay})";
    }
}
