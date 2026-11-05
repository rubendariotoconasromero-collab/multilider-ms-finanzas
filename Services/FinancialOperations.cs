using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.DTOs;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Services;
public sealed record PrepareFinance(Guid CommandId,VentaFinanciera Venta);
public sealed class FinancialOperations(AppDbContext db) {
    public async Task<FinancialOperation> Prepare(OperationAuthorization grant,PrepareFinance request,CancellationToken ct,string? readSnapshot=null) {
        var s=request.Venta;
        Api.Require(grant.Kind=="venta" && grant.ResourceId==s.PropiedadId && request.CommandId!=Guid.Empty,"Operación incompatible.",409);
        Api.Currency(s.Moneda);
        Api.Require(s.Id>0 && s.ClienteId>0 && s.AsesorId>0 && s.MontoLiquido>0 && s.MontoLiquido==Api.Money(s.MontoLiquido) &&
            s.MontoComision>=0 && s.TipoVenta is "CONTADO" or "CREDITO","Condiciones inválidas.");
        if(s.TipoVenta=="CREDITO")Api.Require(s.CuotaInicial>=0 && s.SaldoCredito==s.MontoLiquido-s.CuotaInicial &&
            s.SaldoCredito>0 && s.NumeroCuotas is >=1 and <=600 && s.FechaInicioPago.HasValue,"Plan inconsistente.");
        var hash=OperationProtocol.Hash(s);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await OperationProtocol.Lock(db,"finance:operations",ct);
        var old=await db.Set<FinancialOperation>().FindAsync([grant.OperationId],ct);
        if(old is not null) {
            Api.Require(old.State!="Abortada" && old.SnapshotHash==hash && old.VentaId==s.Id &&
                old.ActorIssuer==grant.ActorIssuer && old.ActorSubject==grant.ActorSubject,"Preparación incompatible.",409);
            return old;
        }
        Api.Require(!await db.VentasAnuladas.AnyAsync(x=>x.NotaVentaId==s.Id,ct) &&
            !await db.CuentasVentas.AnyAsync(x=>x.NotaVentaId==s.Id,ct),"Venta ya registrada o anulada.",409);
        var operation=new FinancialOperation{Id=grant.OperationId,VentaId=s.Id,PropiedadId=s.PropiedadId,
            Snapshot=JsonSerializer.Serialize(s,OperationProtocol.Json),ReadSnapshot=readSnapshot??"",CustomerCi=string.IsNullOrEmpty(readSnapshot)?null:JsonSerializer.Deserialize<JsonElement>(readSnapshot).GetProperty("cliente").GetProperty("ci").GetString(),SnapshotHash=hash,ActorIssuer=grant.ActorIssuer,ActorSubject=grant.ActorSubject};
        if(!string.IsNullOrEmpty(readSnapshot)) {
            var document=JsonSerializer.Deserialize<JsonElement>(readSnapshot);
            operation.CustomerName=document.GetProperty("cliente").GetProperty("nombre_completo").GetString();
            operation.AdviserName=document.GetProperty("asesor").GetProperty("nombre_completo").GetString();
            operation.AdviserEmail=document.GetProperty("asesor").TryGetProperty("correo",out var mail)?mail.GetString():null;
        }
        db.Add(operation);
        var account=new CuentaVenta{NotaVentaId=s.Id,ClienteId=s.ClienteId,AsesorId=s.AsesorId,TipoVenta=s.TipoVenta,
            Moneda=s.Moneda,MontoLiquido=s.MontoLiquido,CuotaInicial=s.CuotaInicial,MontoComision=s.MontoComision,Estado="Preparada"};
        db.CuentasVentas.Add(account);await db.SaveChangesAsync(ct);
        if(s.TipoVenta=="CREDITO") {
            var plan=new PlanPago{CuentaVentaId=account.Id,Monto=s.SaldoCredito,NumeroCuotas=s.NumeroCuotas,
                TasaInteres=s.TasaInteres,TasaMora=s.TasaMora,FechaInicio=s.FechaInicioPago!.Value,FechaFinal=Amortizacion.MonthOverflow(s.FechaInicioPago.Value,s.NumeroCuotas-1)};
            db.PlanesPago.Add(plan);await db.SaveChangesAsync(ct);
            Api.Require(s.CalculationVersion=="laravel-v1","Versión de cálculo no admitida.",409);
            var cuotas=Amortizacion.GenerarLaravel(plan.Monto,plan.NumeroCuotas,plan.TasaInteres,plan.FechaInicio);
            cuotas.ForEach(x=>x.PlanPagoId=plan.Id);db.Cuotas.AddRange(cuotas);
        }
        db.Pagos.Add(new Pago{CuentaVentaId=account.Id,ConceptoPago=s.TipoVenta=="CONTADO"?"VENTA_CONTADO":"CUOTA_INICIAL",
            Monto=s.TipoVenta=="CONTADO"?s.MontoLiquido:s.CuotaInicial});
        if(s.MontoComision>0)db.Egresos.Add(new Egreso{CuentaVentaId=account.Id,AsesorId=s.AsesorId,Fecha=s.Fecha,
            Concepto=$"Comisión venta #{s.Id}",Categoria="COMISION_ASESOR",Origen="AUTOMATICO",Monto=s.MontoComision,Moneda=s.Moneda,UserId=int.Parse(grant.ActorSubject)});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return operation;
    }
    public async Task<FinancialOperation> Activate(Guid id,int property, CancellationToken ct) {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await OperationProtocol.Lock(db,"finance:operations",ct);
        var op=await db.Set<FinancialOperation>().FindAsync([id],ct)??throw new BusinessException("Preparación no encontrada.",404);
        Api.Require(op.PropiedadId==property && op.State is "Preparada" or "Activa","Operación no activable.",409);
        var account=await db.CuentasVentas.SingleAsync(x=>x.NotaVentaId==op.VentaId,ct);
        Api.Require(account.Estado is "Preparada" or "Activa","Cuenta cerrada.",409);
        op.State="Activa";op.UpdatedAt=DateTime.UtcNow;account.Estado="Activa";
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return op;
    }
    public async Task Abort(OperationAuthorization grant,CancellationToken ct) {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await OperationProtocol.Lock(db,"finance:operations",ct);
        var op=await db.Set<FinancialOperation>().FindAsync([grant.OperationId],ct);
        if(op is null){db.Add(new FinancialOperation{Id=grant.OperationId,State="Abortada",PropiedadId=grant.ResourceId});}
        else {
            Api.Require(op.State is "Preparada" or "Abortada","No se aborta una cuenta activada.",409);
            if(op.VentaId>0)await CancelRows(op.VentaId,ct);
            op.State="Abortada";op.UpdatedAt=DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task<FinancialOperation> Close(OperationAuthorization grant,CancellationToken ct) {
        Api.Require(grant.Kind=="anulacion" && grant.State=="Anulando" && grant.ParentOperationId.HasValue,"Falta decisión de anulación autorizada.",409);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await OperationProtocol.Lock(db,"finance:operations",ct);
        if(await db.Set<ClosureRejection>().FindAsync([grant.OperationId],ct) is {} denied) {
            Api.Require(denied.ParentOperationId==grant.ParentOperationId&&denied.PropertyId==grant.ResourceId,"Cierre incompatible.",409);
            throw new BusinessException(denied.Reason,409);
        }
        var op=await db.Set<FinancialOperation>().FindAsync([grant.ParentOperationId!.Value],ct)??throw new BusinessException("Cuenta no encontrada.",404);
        Api.Require(op.PropiedadId==grant.ResourceId && (op.State=="Activa" || op.State=="Cerrada" && op.ClosedBy==grant.OperationId),"Cierre incompatible.",409);
        try {await CancelRows(op.VentaId,ct);}
        catch(BusinessException e) when(e.Status==409) {
            db.Add(new ClosureRejection{Id=grant.OperationId,ParentOperationId=op.Id,PropertyId=op.PropiedadId,Reason=e.Message});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);throw;
        }
        op.State="Cerrada";op.ClosedBy=grant.OperationId;op.UpdatedAt=DateTime.UtcNow;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return op;
    }
    private async Task CancelRows(int ventaId,CancellationToken ct) {
        Api.Require(!await db.Set<PaymentTestObligation>().AnyAsync(x=>x.VentaId==ventaId&&db.Set<PaymentTestOrder>().Any(o=>o.Id==x.OrderId&&o.State!="PagadaPrueba"),ct),"Pago externo con resultado pendiente; concilia antes de anular.",409);
        var account=await db.CuentasVentas.SingleAsync(x=>x.NotaVentaId==ventaId,ct);
        Api.Require(!await db.Pagos.AnyAsync(x=>x.CuentaVentaId==account.Id && x.Estado=="PAGADO",ct),"La venta tiene cobros; requiere resolución autorizada.",409);
        Api.Require(!await db.Egresos.AnyAsync(x=>x.CuentaVentaId==account.Id && x.Estado=="PAGADO",ct),"La venta tiene desembolsos.",409);
        account.Estado="Anulada";
        foreach(var p in await db.Pagos.Where(x=>x.CuentaVentaId==account.Id && x.Estado=="PENDIENTE_PAGO").ToListAsync(ct))p.Estado="ANULADO";
        foreach(var e in await db.Egresos.Where(x=>x.CuentaVentaId==account.Id && x.Estado=="PENDIENTE").ToListAsync(ct))e.Estado="ANULADO";
        var plan=await db.PlanesPago.SingleOrDefaultAsync(x=>x.CuentaVentaId==account.Id,ct);
        if(plan is not null)foreach(var c in await db.Cuotas.Where(x=>x.PlanPagoId==plan.Id && (x.Estado=="Pendiente"||x.Estado=="Vencida")).ToListAsync(ct))c.Estado="Anulada";
        if(!await db.VentasAnuladas.AnyAsync(x=>x.NotaVentaId==ventaId,ct))db.VentasAnuladas.Add(new VentaAnulada{NotaVentaId=ventaId});
    }
}
