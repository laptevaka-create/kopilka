namespace Kopilka.Core.Domain;

/// <summary>
/// Месячный бюджет: лимит на категорию расхода в конкретном месяце.
/// Месяц хранится строкой «ГГГГ-ММ» — как в базе, без отдельного типа.
/// Пара (категория, месяц) уникальна: бюджет на месяц один.
/// </summary>
public sealed record Budget(
    long Id,
    long CategoryId,
    string Month,
    long LimitKopecks);
