namespace Kopilka.Core.Domain;

/// <summary>
/// Шаблон регулярного платежа: категория, счёт, сумма и день месяца.
/// Начисления по шаблону создаёт RecurringEngine (шаг 6);
/// LastApplied хранит дату последнего начисления и защищает от дублей.
/// </summary>
public sealed class RecurringPayment
{
    /// <summary>Идентификатор шаблона.</summary>
    public long Id { get; set; }

    /// <summary>Категория начисления.</summary>
    public long CategoryId { get; set; }

    /// <summary>Навигация на категорию.</summary>
    public Category? Category { get; set; }

    /// <summary>Счёт начисления.</summary>
    public long AccountId { get; set; }

    /// <summary>Навигация на счёт.</summary>
    public Account? Account { get; set; }

    /// <summary>Сумма в копейках, положительная.</summary>
    public long AmountKopecks { get; set; }

    /// <summary>День месяца 1–31; в коротком месяце — последний день.</summary>
    public int DayOfMonth { get; set; }

    /// <summary>Необязательный комментарий.</summary>
    public string? Comment { get; set; }

    /// <summary>Активен ли шаблон.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Дата последнего начисления, защита от дублей.</summary>
    public DateOnly? LastApplied { get; set; }
}
