using Kopilka.Core.Domain;

namespace Kopilka.Core.Data;

/// <summary>Доступ к шаблонам регулярных платежей.</summary>
public interface IRecurringPaymentRepository
{
    /// <summary>Все шаблоны, по возрастанию дня месяца.</summary>
    IReadOnlyList<RecurringPayment> ListAll();

    /// <summary>Шаблон по идентификатору или null.</summary>
    RecurringPayment? GetById(long id);

    /// <summary>Добавляет шаблон, возвращает его идентификатор.</summary>
    long Add(long categoryId, long accountId, long amountKopecks, int dayOfMonth, string? comment, bool isActive);

    /// <summary>Заменяет шаблон. Возвращает false, если его нет.</summary>
    bool Update(RecurringPayment template);

    /// <summary>Удаляет шаблон. Возвращает false, если его нет.</summary>
    bool Delete(long id);
}
