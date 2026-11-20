using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Kopilka.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    has_limit = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.CheckConstraint("ck_categories_kind", "kind IN ('income', 'expense')");
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "budgets",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    category_id = table.Column<long>(type: "INTEGER", nullable: false),
                    month = table.Column<string>(type: "TEXT", nullable: false),
                    limit_kopecks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budgets", x => x.id);
                    table.CheckConstraint("ck_budgets_limit_positive", "limit_kopecks > 0");
                    table.ForeignKey(
                        name: "FK_budgets_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    category_id = table.Column<long>(type: "INTEGER", nullable: false),
                    account_id = table.Column<long>(type: "INTEGER", nullable: false),
                    amount_kopecks = table.Column<long>(type: "INTEGER", nullable: false),
                    day_of_month = table.Column<int>(type: "INTEGER", nullable: false),
                    comment = table.Column<string>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_applied = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring", x => x.id);
                    table.CheckConstraint("ck_recurring_amount_positive", "amount_kopecks > 0");
                    table.CheckConstraint("ck_recurring_day_of_month", "day_of_month BETWEEN 1 AND 31");
                    table.ForeignKey(
                        name: "FK_recurring_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    account_id = table.Column<long>(type: "INTEGER", nullable: false),
                    category_id = table.Column<long>(type: "INTEGER", nullable: false),
                    amount_kopecks = table.Column<long>(type: "INTEGER", nullable: false),
                    date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    comment = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.CheckConstraint("ck_transactions_amount_positive", "amount_kopecks > 0");
                    table.ForeignKey(
                        name: "FK_transactions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "accounts",
                columns: new[] { "id", "name", "sort_order" },
                values: new object[,]
                {
                    { 1L, "Наличные", 0 },
                    { 2L, "Карта", 1 }
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "has_limit", "kind", "name", "sort_order" },
                values: new object[,]
                {
                    { 1L, false, "income", "Зарплата", 0 },
                    { 2L, false, "income", "Прочие доходы", 1 },
                    { 3L, false, "expense", "Продукты", 0 },
                    { 4L, false, "expense", "Транспорт", 1 },
                    { 5L, false, "expense", "Развлечения", 2 },
                    { 6L, false, "expense", "Прочие расходы", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_name",
                table: "accounts",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_budgets_category_month",
                table: "budgets",
                columns: new[] { "category_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_name_kind",
                table: "categories",
                columns: new[] { "kind", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_account_id",
                table: "recurring",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_active",
                table: "recurring",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_category_id",
                table: "recurring",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_account_date",
                table: "transactions",
                columns: new[] { "account_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_date",
                table: "transactions",
                columns: new[] { "category_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_date",
                table: "transactions",
                column: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budgets");

            migrationBuilder.DropTable(
                name: "recurring");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
