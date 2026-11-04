using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Data;
public sealed class AppDbContext(DbContextOptions<AppDbContext> options):ServiceDbContext(options) {
    public DbSet<CuentaVenta> CuentasVentas=>Set<CuentaVenta>();
    public DbSet<VentaAnulada> VentasAnuladas=>Set<VentaAnulada>();
    public DbSet<PlanPago> PlanesPago=>Set<PlanPago>();
    public DbSet<Cuota> Cuotas=>Set<Cuota>();
    public DbSet<Reprogramacion> Reprogramaciones=>Set<Reprogramacion>();
    public DbSet<Pago> Pagos=>Set<Pago>();
    public DbSet<Ingreso> Ingresos=>Set<Ingreso>();
    public DbSet<Egreso> Egresos=>Set<Egreso>();
    public DbSet<MetodoPago> MetodosPago=>Set<MetodoPago>();
    public DbSet<CuentaBancaria> CuentasBancarias=>Set<CuentaBancaria>();
    protected override void OnModelCreating(ModelBuilder m) {
        m.Entity<CuentaVenta>().HasIndex(x=>x.NotaVentaId).IsUnique();
        m.Entity<VentaAnulada>().HasIndex(x=>x.NotaVentaId).IsUnique();
        m.Entity<PlanPago>().HasIndex(x=>x.CuentaVentaId).IsUnique();
        m.Entity<PlanPago>().HasOne<CuentaVenta>().WithMany().HasForeignKey(x=>x.CuentaVentaId);
        m.Entity<Cuota>().HasOne<PlanPago>().WithMany().HasForeignKey(x=>x.PlanPagoId);
        m.Entity<Cuota>().HasOne<Reprogramacion>().WithMany().HasForeignKey(x=>x.ReprogramacionId);
        m.Entity<Reprogramacion>().HasOne<PlanPago>().WithMany().HasForeignKey(x=>x.PlanPagoId);
        m.Entity<Reprogramacion>().HasIndex(x=>x.SolicitudId).IsUnique();
        m.Entity<Pago>().HasIndex(x=>x.SolicitudId).IsUnique().HasFilter("[SolicitudId] IS NOT NULL");
        m.Entity<Pago>().HasOne<CuentaVenta>().WithMany().HasForeignKey(x=>x.CuentaVentaId);
        m.Entity<Pago>().HasOne<Cuota>().WithMany().HasForeignKey(x=>x.CuotaId);
        m.Entity<Pago>().HasOne<MetodoPago>().WithMany().HasForeignKey(x=>x.MetodoPagoId);
        m.Entity<Pago>().HasOne<CuentaBancaria>().WithMany().HasForeignKey(x=>x.CuentaId);
        m.Entity<Ingreso>().HasOne<Pago>().WithMany().HasForeignKey(x=>x.PagoId);
        m.Entity<Ingreso>().HasOne<CuentaVenta>().WithMany().HasForeignKey(x=>x.CuentaVentaId);
        m.Entity<Ingreso>().HasOne<CuentaBancaria>().WithMany().HasForeignKey(x=>x.CuentaBancariaId);
        m.Entity<Ingreso>().HasIndex(x=>x.PagoId).IsUnique().HasFilter("[PagoId] IS NOT NULL");
        m.Entity<Egreso>().HasOne<CuentaVenta>().WithMany().HasForeignKey(x=>x.CuentaVentaId);
        m.Entity<Egreso>().HasOne<CuentaBancaria>().WithMany().HasForeignKey(x=>x.CuentaBancariaId);
        m.Entity<Egreso>().HasIndex(x=>new{x.CuentaVentaId,x.Categoria}).IsUnique()
            .HasFilter("[CuentaVentaId] IS NOT NULL AND [Categoria] = 'COMISION_ASESOR'");
        base.OnModelCreating(m);
        m.Entity<Egreso>().Property(x=>x.Categoria).HasMaxLength(100);
    }
}

