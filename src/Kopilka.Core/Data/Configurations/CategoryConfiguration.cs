using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kopilka.Core.Data.Configurations;

/// <summary>
/// Категории: тип хранится текстом «income»/«expense» (конвертер),
/// имя уникально внутри типа. Предустановленный набор — часть модели.
/// </summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", table => table.HasCheckConstraint(
            "ck_categories_kind", "kind IN ('income', 'expense')"));

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").IsRequired();
        builder.Property(c => c.Kind).HasColumnName("kind")
            .HasConversion(
                kind => kind == CategoryKind.Income ? "income" : "expense",
                raw => raw == "income" ? CategoryKind.Income : CategoryKind.Expense);
        builder.Property(c => c.HasLimit).HasColumnName("has_limit");
        builder.Property(c => c.SortOrder).HasColumnName("sort_order");

        builder.HasIndex(c => new { c.Kind, c.Name })
            .IsUnique()
            .HasDatabaseName("ix_categories_name_kind");

        builder.HasData(
            new Category { Id = 1, Name = "Зарплата", Kind = CategoryKind.Income, HasLimit = false, SortOrder = 0 },
            new Category { Id = 2, Name = "Прочие доходы", Kind = CategoryKind.Income, HasLimit = false, SortOrder = 1 },
            new Category { Id = 3, Name = "Продукты", Kind = CategoryKind.Expense, HasLimit = false, SortOrder = 0 },
            new Category { Id = 4, Name = "Транспорт", Kind = CategoryKind.Expense, HasLimit = false, SortOrder = 1 },
            new Category { Id = 5, Name = "Развлечения", Kind = CategoryKind.Expense, HasLimit = false, SortOrder = 2 },
            new Category { Id = 6, Name = "Прочие расходы", Kind = CategoryKind.Expense, HasLimit = false, SortOrder = 3 });
    }
}
