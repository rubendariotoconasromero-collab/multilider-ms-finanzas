using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Services;
public sealed class FinanceReads(AppDbContext db,FinanzasService service) {
    public static JsonObject Node(object value)=>JsonSerializer.SerializeToNode(value,OperationProtocol.Json)!.AsObject();
    public async Task<JsonObject> Sale(CuentaVenta account,CancellationToken ct) {
        var operation=await db.Set<FinancialOperation>().AsNoTracking().SingleOrDefaultAsync(x=>x.VentaId==account.NotaVentaId,ct);
        var result=operation is null?Node(account):JsonNode.Parse(operation.Snapshot)!.AsObject();
        result["id"]=account.NotaVentaId;result["estado"]=account.Estado;
        if(!string.IsNullOrEmpty(operation?.ReadSnapshot)) {
            var document=JsonNode.Parse(operation.ReadSnapshot)!.AsObject();
            foreach(var key in new[]{"cliente","asesor","propiedad"})result[key]=document[key]?.DeepClone();
            if(result["propiedad"] is JsonObject property)property.Remove("propietarios");
        }
        return result;
    }
    public async Task<JsonObject> Expense(Egreso expense,CancellationToken ct) {
        var result=Node(expense);
        if(expense.CuentaVentaId is int account) {
            var sale=await Api.Find(db.CuentasVentas,account,ct);result["nota_venta_id"]=sale.NotaVentaId;result["nota_venta"]=await Sale(sale,ct);
            result["asesor"]=result["nota_venta"]?["asesor"]?.DeepClone();
        }
        if(expense.CuentaBancariaId is int bank)result["cuenta_bancaria"]=Node(await Api.Find(db.CuentasBancarias,bank,ct));
        return result;
    }
    public async Task<JsonObject> Income(Ingreso income,CancellationToken ct) {
        var result=Node(income);
        if(income.CuentaVentaId is int account) {var sale=await Api.Find(db.CuentasVentas,account,ct);result["nota_venta_id"]=sale.NotaVentaId;result["nota_venta"]=await Sale(sale,ct);}
        if(income.CuentaBancariaId is int bank)result["cuenta_bancaria"]=Node(await Api.Find(db.CuentasBancarias,bank,ct));
        if(income.PagoId is int payment)result["pago"]=Node(await Api.Find(db.Pagos,payment,ct));
        return result;
    }
    public async Task<JsonObject> Payment(Pago payment,CancellationToken ct) {
        var result=Node(payment);var account=await Api.Find(db.CuentasVentas,payment.CuentaVentaId,ct);
        result["nota_venta_id"]=account.NotaVentaId;result["nota_venta"]=await Sale(account,ct);
        if(payment.MetodoPagoId is int method) {
            var m=await Api.Find(db.MetodosPago,method,ct);var dto=Node(m);dto["nombre_metodo"]=m.Nombre;result["metodo_pago"]=dto;
        }
        if(payment.CuentaId is int bank)result["cuenta"]=Node(await Api.Find(db.CuentasBancarias,bank,ct));
        return result;
    }
    private static JsonObject PlanNode(PlanPago plan,int venta,IEnumerable<Cuota> cuotas,Dictionary<int,InstallmentBalance> balances) {
        var node=Node(plan);node["nota_venta_id"]=venta;var data=new JsonArray();
        foreach(var q in cuotas) {
            var entry=Node(q);foreach(var field in Node(balances[q.Id]))entry[field.Key]=field.Value?.DeepClone();data.Add(entry);
        }
        node["cuotas"]=data;return node;
    }
    public async Task<object[]> Accounts(IReadOnlyList<CuentaVenta> accounts, bool includePayments, CancellationToken ct)
    {
        var accountIds = accounts.Select(x => x.Id).ToArray();
        var saleIds = accounts.Select(x => x.NotaVentaId).ToArray();
        var operations = await db.Set<FinancialOperation>().AsNoTracking()
            .Where(x => saleIds.Contains(x.VentaId) && x.State == "Activa").ToDictionaryAsync(x => x.VentaId, ct);
        var plans = await db.PlanesPago.AsNoTracking().Where(x => accountIds.Contains(x.CuentaVentaId))
            .ToDictionaryAsync(x => x.CuentaVentaId, ct);
        var planIds = plans.Values.Select(x => x.Id).ToArray();
        var quotaRows = await db.Cuotas.AsNoTracking().Where(x => planIds.Contains(x.PlanPagoId))
            .OrderBy(x => x.NumeroCuota).ThenBy(x => x.Id).ToListAsync(ct);
        var quotas = quotaRows.ToLookup(x => x.PlanPagoId);
        var payments = await db.Pagos.AsNoTracking().Where(x => accountIds.Contains(x.CuentaVentaId) && x.Estado == "PAGADO")
            .OrderBy(x => x.FechaPago).ThenBy(x => x.Id).ToListAsync(ct);
        var byAccount = payments.ToLookup(x => x.CuentaVentaId);
        return accounts.Select(account =>
        {
            operations.TryGetValue(account.NotaVentaId, out var operation);
            var document = string.IsNullOrEmpty(operation?.ReadSnapshot) ? null : JsonNode.Parse(operation.ReadSnapshot)!.AsObject();
            var snapshot = string.IsNullOrEmpty(operation?.Snapshot) ? null : JsonNode.Parse(operation.Snapshot);
            var property = document?["propiedad"]?.AsObject();
            property?.Remove("propietarios");
            var rows = byAccount[account.Id];
            JsonObject? planNode = null;
            var remaining = 0m;
            if (plans.TryGetValue(account.Id, out var plan))
            {
                var schedule = quotas[plan.Id].ToArray();
                var paid = rows.Where(x => x.ConceptoPago == "CUOTA" && x.CuotaId.HasValue)
                    .GroupBy(x => x.CuotaId!.Value).ToDictionary(x => x.Key, x => x.Sum(p => p.Monto));
                var balances = schedule.ToDictionary(x => x.Id, x => InstallmentBalance.Calculate(x, paid.GetValueOrDefault(x.Id)));
                remaining = schedule.Where(x => x.Estado is "Pendiente" or "Vencida").Sum(x => balances[x.Id].CapitalPendiente);
                planNode = PlanNode(plan, account.NotaVentaId, schedule, balances);
            }
            return (object)new
            {
                id = account.NotaVentaId, account.ClienteId, account.AsesorId, account.TipoVenta, account.Moneda,
                account.MontoLiquido, account.CuotaInicial,
                monto_total = property?["precio_venta"]?.GetValue<decimal>() ?? account.MontoLiquido,
                fecha = snapshot?["fecha"]?.GetValue<string>(),
                saldo_credito = remaining, estado = "Completada", propiedad = property,
                cliente = new { id = account.ClienteId, nombre_completo = operation?.CustomerName },
                asesor = new { id = account.AsesorId, nombre_completo = operation?.AdviserName },
                plan_pago = planNode,
                pagos = includePayments ? rows.Select(x => new { x.Id, x.CuotaId, x.ConceptoPago, x.FechaPago, x.Monto, x.Estado }) : null
            };
        }).ToArray();
    }

    public async Task<object> Plan(int venta,CancellationToken ct) {
        var account=await service.Cuenta(venta,ct);var plan=await db.PlanesPago.SingleOrDefaultAsync(x=>x.CuentaVentaId==account.Id,ct)??throw new BusinessException("Venta sin plan.",404);
        var cuotas=await db.Cuotas.Where(x=>x.PlanPagoId==plan.Id).OrderBy(x=>x.NumeroCuota).ThenBy(x=>x.Id).ToListAsync(ct);
        var balances=await InstallmentBalance.Read(db,plan,cuotas,ct);var pending=cuotas.Where(x=>x.Estado is "Pendiente" or "Vencida").ToArray();
        var node=PlanNode(plan,venta,cuotas,balances);
        var history=await db.Reprogramaciones.Where(x=>x.PlanPagoId==plan.Id).OrderBy(x=>x.Id).ToListAsync(ct);
        node["reprogramaciones"]=JsonSerializer.SerializeToNode(history,OperationProtocol.Json);
        return new{plan_pago=node,resumen=new{cuotas_pagadas=cuotas.Count(x=>x.Estado=="Pagada"),cuotas_pendientes=pending.Length,total_reprogramaciones=history.Count,
            total_pagado=balances.Values.Sum(x=>x.MontoPagado),total_pendiente=pending.Sum(x=>balances[x.Id].MontoPendiente),saldo_capital=pending.Sum(x=>balances[x.Id].CapitalPendiente)}};
    }
    public async Task<object> Summary(int venta,CancellationToken ct) {
        var account=await service.Cuenta(venta,ct);var pagos=await db.Pagos.Where(x=>x.CuentaVentaId==account.Id).ToListAsync(ct);
        var data=new List<JsonObject>();foreach(var p in pagos)data.Add(await Payment(p,ct));
        var plan=await db.PlanesPago.SingleOrDefaultAsync(x=>x.CuentaVentaId==account.Id,ct);
        var cuotas=plan is null?[]:await db.Cuotas.Where(x=>x.PlanPagoId==plan.Id).OrderBy(x=>x.NumeroCuota).ToListAsync(ct);
        var balances=plan is null?new Dictionary<int,InstallmentBalance>():await InstallmentBalance.Read(db,plan,cuotas,ct);
        var pending=cuotas.Where(x=>x.Estado is "Pendiente" or "Vencida").ToArray();
        return new{nota_venta=await Sale(account,ct),pagos=data,plan_pago=plan is null?null:PlanNode(plan,venta,cuotas,balances),resumen=new{
            total_pagado=pagos.Where(x=>x.Estado=="PAGADO").Sum(x=>x.Monto),saldo_capital=pending.Sum(x=>balances[x.Id].CapitalPendiente),
            monto_pendiente=pagos.Where(x=>x.Estado=="PENDIENTE_PAGO"&&x.CuotaId==null).Sum(x=>x.Monto)+pending.Sum(x=>balances[x.Id].MontoPendiente)}};
    }
}
