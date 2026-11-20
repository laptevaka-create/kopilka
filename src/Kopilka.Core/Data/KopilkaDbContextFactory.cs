using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kopilka.Core.Data;

/// <summary>
/// Фабрика контекста для design-time: инструмент dotnet ef создаёт
/// контекст при генерации миграций. Файл kopilka-design.db — только
/// для этих целей, в рантайме не используется.
/// </summary>
public sealed class KopilkaDbContextFactory : IDesignTimeDbContextFactory<KopilkaDbContext>
{
    public KopilkaDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<KopilkaDbContext> options = new DbContextOptionsBuilder<KopilkaDbContext>()
            .UseSqlite("Data Source=kopilka-design.db")
            .Options;

        return new KopilkaDbContext(options);
    }
}
