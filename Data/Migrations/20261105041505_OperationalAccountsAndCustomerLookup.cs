using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMultilider.Finanzas.Data.Migrations
{
    /// <inheritdoc />
    public partial class OperationalAccountsAndCustomerLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerCi",
                table: "FinancialOperation",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoIntegracion",
                table: "CuentasBancarias",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "CuentasBancarias",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "CuentasBancarias",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Proveedor",
                table: "CuentasBancarias",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "CuentasBancarias",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperation_CustomerCi_State",
                table: "FinancialOperation",
                columns: new[] { "CustomerCi", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.InvalidOperationException("F5 durable records require coordinated restore instead of a destructive down migration.");
        }
    }
}
