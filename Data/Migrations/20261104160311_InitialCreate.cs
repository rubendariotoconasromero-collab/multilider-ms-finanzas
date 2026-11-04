using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMultilider.Finanzas.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuentasBancarias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Banco = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NumeroCuenta = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Titular = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SaldoInicial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasBancarias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CuentasVentas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotaVentaId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    AsesorId = table.Column<int>(type: "int", nullable: false),
                    TipoVenta = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MontoLiquido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CuotaInicial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoComision = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasVentas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetodosPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Estado = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetodosPago", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VentasAnuladas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotaVentaId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VentasAnuladas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Egresos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CuentaVentaId = table.Column<int>(type: "int", nullable: true),
                    AsesorId = table.Column<int>(type: "int", nullable: true),
                    CuentaBancariaId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Beneficiario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Egresos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Egresos_CuentasBancarias_CuentaBancariaId",
                        column: x => x.CuentaBancariaId,
                        principalTable: "CuentasBancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Egresos_CuentasVentas_CuentaVentaId",
                        column: x => x.CuentaVentaId,
                        principalTable: "CuentasVentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanesPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CuentaVentaId = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NumeroCuotas = table.Column<int>(type: "int", nullable: false),
                    TasaInteres = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TasaMora = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinal = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanesPago_CuentasVentas_CuentaVentaId",
                        column: x => x.CuentaVentaId,
                        principalTable: "CuentasVentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reprogramaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanPagoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FechaReprogramacion = table.Column<DateOnly>(type: "date", nullable: false),
                    CuotaDesde = table.Column<int>(type: "int", nullable: false),
                    SaldoCapitalMomento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NuevaTasaInteres = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NuevoNumeroCuotas = table.Column<int>(type: "int", nullable: false),
                    NuevaFechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reprogramaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reprogramaciones_PlanesPago_PlanPagoId",
                        column: x => x.PlanPagoId,
                        principalTable: "PlanesPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanPagoId = table.Column<int>(type: "int", nullable: false),
                    NumeroCuota = table.Column<int>(type: "int", nullable: false),
                    ReprogramacionId = table.Column<int>(type: "int", nullable: true),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    MontoCuota = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoInteres = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cuotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cuotas_PlanesPago_PlanPagoId",
                        column: x => x.PlanPagoId,
                        principalTable: "PlanesPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cuotas_Reprogramaciones_ReprogramacionId",
                        column: x => x.ReprogramacionId,
                        principalTable: "Reprogramaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pagos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CuentaVentaId = table.Column<int>(type: "int", nullable: false),
                    CuotaId = table.Column<int>(type: "int", nullable: true),
                    MetodoPagoId = table.Column<int>(type: "int", nullable: true),
                    CuentaId = table.Column<int>(type: "int", nullable: true),
                    ConceptoPago = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FechaPago = table.Column<DateOnly>(type: "date", nullable: true),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagos_CuentasBancarias_CuentaId",
                        column: x => x.CuentaId,
                        principalTable: "CuentasBancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_CuentasVentas_CuentaVentaId",
                        column: x => x.CuentaVentaId,
                        principalTable: "CuentasVentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_Cuotas_CuotaId",
                        column: x => x.CuotaId,
                        principalTable: "Cuotas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_MetodosPago_MetodoPagoId",
                        column: x => x.MetodoPagoId,
                        principalTable: "MetodosPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ingresos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PagoId = table.Column<int>(type: "int", nullable: true),
                    CuentaVentaId = table.Column<int>(type: "int", nullable: true),
                    CuentaBancariaId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingresos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ingresos_CuentasBancarias_CuentaBancariaId",
                        column: x => x.CuentaBancariaId,
                        principalTable: "CuentasBancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingresos_CuentasVentas_CuentaVentaId",
                        column: x => x.CuentaVentaId,
                        principalTable: "CuentasVentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingresos_Pagos_PagoId",
                        column: x => x.PagoId,
                        principalTable: "Pagos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasVentas_NotaVentaId",
                table: "CuentasVentas",
                column: "NotaVentaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_PlanPagoId",
                table: "Cuotas",
                column: "PlanPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_ReprogramacionId",
                table: "Cuotas",
                column: "ReprogramacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Egresos_CuentaBancariaId",
                table: "Egresos",
                column: "CuentaBancariaId");

            migrationBuilder.CreateIndex(
                name: "IX_Egresos_CuentaVentaId_Categoria",
                table: "Egresos",
                columns: new[] { "CuentaVentaId", "Categoria" },
                unique: true,
                filter: "[CuentaVentaId] IS NOT NULL AND [Categoria] = 'COMISION_ASESOR'");

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_CuentaBancariaId",
                table: "Ingresos",
                column: "CuentaBancariaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_CuentaVentaId",
                table: "Ingresos",
                column: "CuentaVentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_PagoId",
                table: "Ingresos",
                column: "PagoId",
                unique: true,
                filter: "[PagoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_CuentaId",
                table: "Pagos",
                column: "CuentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_CuentaVentaId",
                table: "Pagos",
                column: "CuentaVentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_CuotaId",
                table: "Pagos",
                column: "CuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_MetodoPagoId",
                table: "Pagos",
                column: "MetodoPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_SolicitudId",
                table: "Pagos",
                column: "SolicitudId",
                unique: true,
                filter: "[SolicitudId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlanesPago_CuentaVentaId",
                table: "PlanesPago",
                column: "CuentaVentaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reprogramaciones_PlanPagoId",
                table: "Reprogramaciones",
                column: "PlanPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reprogramaciones_SolicitudId",
                table: "Reprogramaciones",
                column: "SolicitudId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VentasAnuladas_NotaVentaId",
                table: "VentasAnuladas",
                column: "NotaVentaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Egresos");

            migrationBuilder.DropTable(
                name: "Ingresos");

            migrationBuilder.DropTable(
                name: "VentasAnuladas");

            migrationBuilder.DropTable(
                name: "Pagos");

            migrationBuilder.DropTable(
                name: "CuentasBancarias");

            migrationBuilder.DropTable(
                name: "Cuotas");

            migrationBuilder.DropTable(
                name: "MetodosPago");

            migrationBuilder.DropTable(
                name: "Reprogramaciones");

            migrationBuilder.DropTable(
                name: "PlanesPago");

            migrationBuilder.DropTable(
                name: "CuentasVentas");
        }
    }
}
