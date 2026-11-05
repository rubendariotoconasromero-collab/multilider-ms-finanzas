using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMultilider.Finanzas.Data.Migrations
{
    /// <inheritdoc />
    public partial class DurableFinancialOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialOperation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    PropiedadId = table.Column<int>(type: "int", nullable: false),
                    ClosedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Snapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorIssuer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorSubject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialOperation", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperation_VentaId",
                table: "FinancialOperation",
                column: "VentaId",
                unique: true,
                filter: "[VentaId]>0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.InvalidOperationException("F5 durable records require coordinated restore instead of a destructive down migration.");
        }
    }
}
