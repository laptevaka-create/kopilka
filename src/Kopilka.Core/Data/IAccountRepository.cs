using Kopilka.Core.Domain;

namespace Kopilka.Core.Data;

/// <summary>Доступ к справочнику счетов.</summary>
public interface IAccountRepository
{
    /// <summary>Все счета по порядку sort_order, затем по имени.</summary>
    IReadOnlyList<Account> ListAll();

    /// <summary>Счёт по идентификатору или null.</summary>
    Account? GetById(long id);

    /// <summary>Добавляет счёт, возвращает его идентификатор.</summary>
    long Add(string name, int sortOrder);

    /// <summary>Переименовывает счёт и меняет порядок. Возвращает false, если счёта нет.</summary>
    bool Update(Account account);

    /// <summary>Удаляет счёт; по используемому счёту — отказ с причиной.</summary>
    DeleteResult Delete(long id);
}
