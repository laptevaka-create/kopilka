using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kopilka.Core.Data.Configurations;

/// <summary>Бюджеты: одна строка на пару (категория, месяц).</summary>
public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("budgets", table => table.HasCheckConstraint(
            "ck_budgets_limit_positive", "limit_kopecks > 0"));

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.CategoryId).HasColumnName("category_id");
        builder.Property(b => b.Month).HasColumnName("month").IsRequired();
        builder.Property(b => b.LimitKopecks).HasColumnName("limit_kopecks");

        builder.HasOne(b => b.Category)
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.CategoryId, b.Month })
            .IsUnique()
            .HasDatabaseName("ix_budgets_category_month");
    }
}
