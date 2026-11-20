using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kopilka.Core.Data;

/// <summary>
/// Контекст данных Entity Framework Core: единая точка описания схемы.
/// Конфигурации сущностей — в Data/Configurations, применяются
/// автоматически. Миграции EF Core создаются инструментом dotnet ef
/// и применяются методом Database.Migrate().
/// </summary>
public sealed class KopilkaDbContext(DbContextOptions<KopilkaDbContext> options) : DbContext(options)
{
    /// <summary>Справочник счетов.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>Справочник категорий.</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>Журнал транзакций.</summary>
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>Месячные бюджеты.</summary>
    public DbSet<Budget> Budgets => Set<Budget>();

    /// <summary>Шаблоны регулярных платежей.</summary>
    public DbSet<RecurringPayment> RecurringPayments => Set<RecurringPayment>();

    /// <summary>Настройки «ключ—значение».</summary>
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KopilkaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
