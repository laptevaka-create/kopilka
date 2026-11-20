namespace Kopilka.Core.Domain;

/// <summary>Тип категории: доход или расход.</summary>
public enum CategoryKind
{
    Income,
    Expense,
}

/// <summary>
/// Категория дохода или расхода. Категория расхода с HasLimit = true
/// участвует в бюджетах; для категории без лимита бюджет не задаётся.
/// </summary>
public sealed class Category
{
    /// <summary>Идентификатор категории.</summary>
    public long Id { get; set; }

    /// <summary>Имя, уникально внутри типа (доход/расход).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Тип: доход или расход.</summary>
    public CategoryKind Kind { get; set; }

    /// <summary>Признак «по категории задаётся месячный бюджет».</summary>
    public bool HasLimit { get; set; }

    /// <summary>Порядок отображения в списках.</summary>
    public int SortOrder { get; set; }
}
