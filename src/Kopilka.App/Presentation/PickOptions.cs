using Kopilka.Core.Domain;

namespace Kopilka.App.Presentation;

/// <summary>Вариант счёта для выпадающих списков.</summary>
public sealed record AccountPick(long Id, string Name);

/// <summary>Вариант категории для выпадающих списков, с типом.</summary>
public sealed record CategoryPick(long Id, string Name, CategoryKind Kind)
{
    /// <summary>Подпись в списке: «Продукты (расход)».</summary>
    public string Display => $"{Name} ({(Kind == CategoryKind.Income ? "доход" : "расход")})";
}
