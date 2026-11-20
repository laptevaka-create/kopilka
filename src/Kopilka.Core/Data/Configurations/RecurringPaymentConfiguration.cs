using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kopilka.Core.Data.Configurations;

/// <summary>Шаблоны регулярных платежей: день месяца 1–31, сумма положительна.</summary>
public sealed class RecurringPaymentConfiguration : IEntityTypeConfiguration<RecurringPayment>
{
    public void Configure(EntityTypeBuilder<RecurringPayment> builder)
    {
        builder.ToTable("recurring", table =>
        {
            table.HasCheckConstraint("ck_recurring_amount_positive", "amount_kopecks > 0");
            table.HasCheckConstraint("ck_recurring_day_of_month", "day_of_month BETWEEN 1 AND 31");
        });

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.CategoryId).HasColumnName("category_id");
        builder.Property(r => r.AccountId).HasColumnName("account_id");
        builder.Property(r => r.AmountKopecks).HasColumnName("amount_kopecks");
        builder.Property(r => r.DayOfMonth).HasColumnName("day_of_month");
        builder.Property(r => r.Comment).HasColumnName("comment");
        builder.Property(r => r.IsActive).HasColumnName("is_active");
        builder.Property(r => r.LastApplied).HasColumnName("last_applied");

        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Account)
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.IsActive).HasDatabaseName("ix_recurring_active");
    }
}
