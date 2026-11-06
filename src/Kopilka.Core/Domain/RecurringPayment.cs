namespace Kopilka.Core.Domain;

/// <summary>
/// Шаблон регулярного платежа: категория, счёт, сумма и день месяца.
/// Начисления по шаблону создаёт RecurringEngine (шаг 6);
/// LastApplied хранит дату последнего начисления и защищает от дублей.
/// </summary>
public sealed record RecurringPayment(
    long Id,
    long CategoryId,
    long AccountId,
    long AmountKopecks,
    int DayOfMonth,
    string? Comment,
    bool IsActive,
    DateOnly? LastApplied);
