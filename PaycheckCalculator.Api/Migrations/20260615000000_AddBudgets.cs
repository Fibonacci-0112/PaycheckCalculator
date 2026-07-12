using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaycheckCalculator.Api.Migrations
{
    /// <inheritdoc />
    public partial class _20260615000000_AddBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    UserId   = table.Column<string>(type: "text", nullable: false),
                    NameKey  = table.Column<string>(type: "text", nullable: false),
                    Name     = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted    = table.Column<bool>(type: "boolean", nullable: false),
                    PayloadJson  = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => new { x.UserId, x.NameKey });
                });

            migrationBuilder.CreateTable(
                name: "BudgetTransactions",
                columns: table => new
                {
                    UserId       = table.Column<string>(type: "text", nullable: false),
                    Id           = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted    = table.Column<bool>(type: "boolean", nullable: false),
                    PayloadJson  = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetTransactions", x => new { x.UserId, x.Id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BudgetTransactions");
            migrationBuilder.DropTable(name: "Budgets");
        }
    }
}
