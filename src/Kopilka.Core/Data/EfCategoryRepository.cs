using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kopilka.Core.Data;

/// <summary>Справочник категорий поверх EF Core.</summary>
public sealed class EfCategoryRepository : ICategoryRepository
{
    private readonly IDbContextFactory<KopilkaDbContext> _contextFactory;

    public EfCategoryRepository(IDbContextFactory<KopilkaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<Category> ListAll()
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        // Расходы раньше доходов: списку интерфейса так группироваться проще
        // (expense < income при текстовом хранении типа).
        return context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public Category? GetById(long id)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();

        return context.Categories.AsNoTracking().FirstOrDefault(c => c.Id == id);
    }

    public long Add(string name, CategoryKind kind, bool hasLimit, int sortOrder)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        var category = new Category { Name = name, Kind = kind, HasLimit = hasLimit, SortOrder = sortOrder };
        context.Categories.Add(category);
        context.SaveChanges();

        return category.Id;
    }

    public bool Update(Category category)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        Category? existing = context.Categories.Find(category.Id);
        if (existing is null)
        {
            return false;
        }

        existing.Name = category.Name;
        existing.Kind = category.Kind;
        existing.HasLimit = category.HasLimit;
        existing.SortOrder = category.SortOrder;
        context.SaveChanges();

        return true;
    }

    public DeleteResult Delete(long id)
    {
        using KopilkaDbContext context = _contextFactory.CreateDbContext();
        // Использование — все ссылки на категорию: транзакции,
        // бюджеты и шаблоны регулярных платежей. Иначе репозиторий
        // дошёл бы до SaveChanges и упал необработанным исключением.
        if (context.Transactions.Any(t => t.CategoryId == id)
            || context.Budgets.Any(b => b.CategoryId == id)
            || context.RecurringPayments.Any(r => r.CategoryId == id))
        {
            return DeleteResult.InUse;
        }

        Category? existing = context.Categories.Find(id);
        if (existing is null)
        {
            return DeleteResult.NotFound;
        }

        context.Categories.Remove(existing);
        context.SaveChanges();

        return DeleteResult.Deleted;
    }
}
