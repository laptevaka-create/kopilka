using Kopilka.Core.Domain;

namespace Kopilka.Core.Data;

/// <summary>Доступ к журналу транзакций.</summary>
public interface ITransactionRepository
{
    /// <summary>Добавляет транзакцию, возвращает её идентификатор.</summary>
    long Add(long accountId, long categoryId, long amountKopecks, DateOnly date, string? comment);

    /// <summary>Транзакция по идентификатору или null.</summary>
    Transaction? GetById(long id);

    /// <summary>
    /// Транзакции за период [from, to] включительно, отсортированные
    /// по дате, затем по идентификатору. Если categoryId задан —
    /// только транзакции этой категории.
    /// </summary>
    IReadOnlyList<Transaction> ListByPeriod(DateOnly from, DateOnly to, long? categoryId = null);

    /// <summary>Заменяет транзакцию. Возвращает false, если её нет.</summary>
    bool Update(Transaction transaction);

    /// <summary>Удаляет транзакцию. Возвращает false, если её нет.</summary>
    bool Delete(long id);
}
