using Kopilka.Core.Domain;

namespace Kopilka.Core.Data;

/// <summary>Доступ к справочнику категорий.</summary>
public interface ICategoryRepository
{
    /// <summary>Все категории: сначала расходы, затем доходы; внутри типа — по sort_order, затем по имени.</summary>
    IReadOnlyList<Category> ListAll();

    /// <summary>Категория по идентификатору или null.</summary>
    Category? GetById(long id);

    /// <summary>Добавляет категорию, возвращает её идентификатор.</summary>
    long Add(string name, CategoryKind kind, bool hasLimit, int sortOrder);

    /// <summary>Меняет имя, признак лимита и порядок. Возвращает false, если категории нет.</summary>
    bool Update(Category category);

    /// <summary>Удаляет категорию; по используемой категории — отказ с причиной.</summary>
    DeleteResult Delete(long id);
}
