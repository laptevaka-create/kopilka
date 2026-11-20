namespace Kopilka.Core.Domain;

/// <summary>
/// Месячный бюджет: лимит на категорию расхода в конкретном месяце.
/// Месяц хранится строкой «ГГГГ-ММ». Пара (категория, месяц) уникальна:
/// бюджет на месяц один.
/// </summary>
public sealed class Budget
{
    /// <summary>Идентификатор бюджета.</summary>
    public long Id { get; set; }

    /// <summary>Категория расхода, по которой задан лимит.</summary>
    public long CategoryId { get; set; }

    /// <summary>Навигация на категорию.</summary>
    public Category? Category { get; set; }

    /// <summary>Месяц, «ГГГГ-ММ».</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Лимит в копейках, положительный.</summary>
    public long LimitKopecks { get; set; }
}
