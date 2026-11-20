namespace Kopilka.Core.Domain;

/// <summary>
/// Одна строка журнала: сумма, счёт, категория, дата и необязательный
/// комментарий. Сумма всегда положительная и хранится в копейках;
/// направление (доход/расход) задаётся типом категории, а не знаком.
/// </summary>
public sealed class Transaction
{
    /// <summary>Идентификатор транзакции.</summary>
    public long Id { get; set; }

    /// <summary>Счёт, по которому прошла операция.</summary>
    public long AccountId { get; set; }

    /// <summary>Навигация на счёт (для запросов EF).</summary>
    public Account? Account { get; set; }

    /// <summary>Категория операции; её тип задаёт доход или расход.</summary>
    public long CategoryId { get; set; }

    /// <summary>Навигация на категорию.</summary>
    public Category? Category { get; set; }

    /// <summary>Сумма в копейках, всегда положительная.</summary>
    public long AmountKopecks { get; set; }

    /// <summary>Дата операции без времени.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Необязательный комментарий.</summary>
    public string? Comment { get; set; }
}
