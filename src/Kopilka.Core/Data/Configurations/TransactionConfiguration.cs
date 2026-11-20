using Kopilka.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kopilka.Core.Data.Configurations;

/// <summary>
/// Транзакции: сумма положительна (CHECK на уровне базы), дата без
/// времени. Ссылки на справочники — Restrict: история неприкосновенна,
/// удаление используемых справочников запрещено.
/// </summary>
public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", table => table.HasCheckConstraint(
            "ck_transactions_amount_positive", "amount_kopecks > 0"));

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.AccountId).HasColumnName("account_id");
        builder.Property(t => t.CategoryId).HasColumnName("category_id");
        builder.Property(t => t.AmountKopecks).HasColumnName("amount_kopecks");
        builder.Property(t => t.Date).HasColumnName("date");
        builder.Property(t => t.Comment).HasColumnName("comment");

        builder.HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Date).HasDatabaseName("ix_transactions_date");
        builder.HasIndex(t => new { t.CategoryId, t.Date }).HasDatabaseName("ix_transactions_category_date");
        builder.HasIndex(t => new { t.AccountId, t.Date }).HasDatabaseName("ix_transactions_account_date");
    }
}
