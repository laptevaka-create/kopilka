namespace Kopilka.Core.Data;

/// <summary>Начисляет транзакции по активным шаблонам за пропущенные даты.</summary>
public interface IRecurringAccruer
{
    /// <summary>
    /// Создаёт начисления по активным шаблонам за даты до toDate
    /// включительно. Шаблоны без начислений (новые) начинают
    /// с today — задним числом не начисляются. Возвращает число
    /// созданных транзакций.
    /// </summary>
    int AccrueUpTo(DateOnly today, DateOnly toDate);
}
