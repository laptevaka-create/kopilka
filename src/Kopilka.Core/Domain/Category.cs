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
public sealed record Category(
    long Id,
    string Name,
    CategoryKind Kind,
    bool HasLimit,
    int SortOrder);
