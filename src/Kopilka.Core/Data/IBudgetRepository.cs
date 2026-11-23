using Kopilka.Core.Domain;

namespace Kopilka.Core.Data;

/// <summary>Доступ к месячным бюджетам категорий.</summary>
public interface IBudgetRepository
{
    /// <summary>Все бюджеты месяца (формат «ГГГГ-ММ»).</summary>
    IReadOnlyList<Budget> ListByMonth(string month);

    /// <summary>Бюджет категории на месяц или null, если не задан.</summary>
    Budget? GetByCategoryAndMonth(long categoryId, string month);

    /// <summary>
    /// Задаёт лимит категории на месяц: создаёт бюджет или обновляет
    /// существующий — пара (категория, месяц) уникальна.
    /// </summary>
    void Set(long categoryId, string month, long limitKopecks);

    /// <summary>Убирает бюджет категории на месяц. Возвращает false, если его не было.</summary>
    bool Remove(long categoryId, string month);
}
