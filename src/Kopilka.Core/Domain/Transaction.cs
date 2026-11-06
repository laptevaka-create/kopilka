namespace Kopilka.Core.Domain;

/// <summary>
/// Одна строка журнала: сумма, счёт, категория, дата и необязательный
/// комментарий. Сумма всегда положительная и хранится в копейках;
/// направление (доход/расход) задаётся типом категории, а не знаком.
/// </summary>
public sealed record Transaction(
    long Id,
    long AccountId,
    long CategoryId,
    long AmountKopecks,
    DateOnly Date,
    string? Comment);
