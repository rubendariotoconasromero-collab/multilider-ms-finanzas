using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Services;

public sealed record InstallmentBalance(decimal MontoPagado,decimal InteresPagado,decimal CapitalPagado,decimal MontoPendiente,decimal InteresPendiente,decimal CapitalPendiente) {
    public static InstallmentBalance Calculate(Cuota quota,decimal paid) {
        paid=Api.Money(Math.Max(0,paid));
        var interest=Math.Min(quota.MontoInteres,paid);
        var capital=Math.Min(quota.MontoCapital,Math.Max(0,paid-interest));
        var closed=quota.Estado=="Pagada"||paid>=quota.MontoCuota;
        return new(paid,interest,capital,closed?0:Math.Max(0,quota.MontoCuota-paid),closed?0:Math.Max(0,quota.MontoInteres-interest),closed?0:Math.Max(0,quota.MontoCapital-capital));
    }
    public static async Task<Dictionary<int,InstallmentBalance>> Read(AppDbContext db,PlanPago plan,IEnumerable<Cuota> quotas,CancellationToken ct) {
        var rows=quotas.ToArray();var ids=rows.Select(x=>x.Id).ToArray();
        var paid=await db.Pagos.AsNoTracking().Where(x=>x.CuentaVentaId==plan.CuentaVentaId&&x.CuotaId.HasValue&&ids.Contains(x.CuotaId.Value)&&x.ConceptoPago=="CUOTA"&&x.Estado=="PAGADO")
            .GroupBy(x=>x.CuotaId!.Value).Select(g=>new{id=g.Key,total=g.Sum(p=>p.Monto)}).ToDictionaryAsync(x=>x.id,x=>x.total,ct);
        return rows.ToDictionary(x=>x.Id,x=>Calculate(x,paid.GetValueOrDefault(x.Id)));
    }
}
