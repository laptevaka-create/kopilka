using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kopilka.Core.Data.Configurations;

/// <summary>
/// Счета: имена таблиц и столбцов прежние (snake_case), имя уникально.
/// Предустановленные счета — часть модели, попадает в миграцию.
/// </summary>
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Name).HasColumnName("name").IsRequired();
        builder.Property(a => a.SortOrder).HasColumnName("sort_order");

        builder.HasIndex(a => a.Name).IsUnique().HasDatabaseName("ix_accounts_name");

        builder.HasData(
            new Account { Id = 1, Name = "Наличные", SortOrder = 0 },
            new Account { Id = 2, Name = "Карта", SortOrder = 1 });
    }
}
