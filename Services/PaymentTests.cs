using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Services;
public sealed record TestPaymentRequest(Guid OperacionId,int ClienteId,int[]? PagoIds,int[]? CuotaIds,
    [Required,StringLength(20)]string CiPagador,[Required,StringLength(20)]string TelefonoPagador,
    [Required,StringLength(100)]string NombresPagador,[Required,StringLength(100)]string ApellidosPagador,
    [Required,EmailAddress,StringLength(150)]string CorreoPagador);
public sealed record TestPaymentLine(int? PagoId,int? CuotaId,int VentaId,decimal MontoReal);
public sealed class PaymentTests(AppDbContext db,ILibelulaProvider provider,IConfiguration config) {
    public static object Dto(PaymentTestOrder o)=>new{id_transaccion=o.Reference,operacion_id=o.Id,url_pago=o.PaymentUrl,qr_url=o.QrUrl,
        estado=o.State=="PagadaPrueba"?"PAGADO_PRUEBA":"PENDIENTE_PAGO",es_prueba=true,monto_prueba=o.Amount,deuda_real_liquidada=false};
    public async Task<PaymentTestOrder> Create(TestPaymentRequest request,CancellationToken ct) {
        Api.Require(request.OperacionId!=Guid.Empty&&request.ClienteId>0,"Datos de operación inválidos.");
        var input=request with{PagoIds=(request.PagoIds??[]).Distinct().Order().ToArray(),CuotaIds=(request.CuotaIds??[]).Distinct().Order().ToArray()};
        Api.Require(input.PagoIds!.Length+input.CuotaIds!.Length is >0 and <=50,"Selecciona entre una y cincuenta obligaciones.");
        var hash=OperationProtocol.Hash(input);PaymentTestOrder order;
        await using(var tx=await db.Database.BeginTransactionAsync(ct)) {
            await OperationProtocol.Lock(db,"finance:operations",ct);
            var old=await db.Set<PaymentTestOrder>().FindAsync([input.OperacionId],ct);
            if(old is not null){Api.Require(old.Hash==hash,"UUID usado con otros datos.",409);return old;}
            Api.Require(config.GetValue<bool>("PaymentTests:Enabled")&&!string.IsNullOrWhiteSpace(config["Libelula:CallbackBaseUrl"])&&!string.IsNullOrWhiteSpace(config["Libelula:FrontendUrl"]),"El registro de pagos de prueba aún no está habilitado.",503);
            provider.EnsureReady();
            var selection=new List<TestPaymentLine>();
            foreach(var id in input.PagoIds) {
                var payment=await Api.Find(db.Pagos,id,ct);var account=await Api.Find(db.CuentasVentas,payment.CuentaVentaId,ct);
                Api.Require(account.Estado=="Activa"&&account.ClienteId==input.ClienteId&&payment.Estado=="PENDIENTE_PAGO"&&payment.Monto>0,"Obligación ajena o no pendiente.",409);
                selection.Add(new(payment.Id,null,account.NotaVentaId,payment.Monto));
            }
            foreach(var id in input.CuotaIds) {
                var quota=await Api.Find(db.Cuotas,id,ct);var plan=await Api.Find(db.PlanesPago,quota.PlanPagoId,ct);var account=await Api.Find(db.CuentasVentas,plan.CuentaVentaId,ct);
                Api.Require(account.Estado=="Activa"&&account.ClienteId==input.ClienteId&&quota.Estado is "Pendiente" or "Vencida","Cuota ajena o no pendiente.",409);
                var remaining=(await InstallmentBalance.Read(db,plan,[quota],ct))[quota.Id].MontoPendiente;
                Api.Require(remaining>0,"Cuota sin saldo.",409);
                selection.Add(new(null,quota.Id,account.NotaVentaId,remaining));
            }
            var sales=selection.Select(x=>x.VentaId).Distinct().ToArray();
            Api.Require(!await db.Set<PaymentTestObligation>().AnyAsync(x=>sales.Contains(x.VentaId)&&db.Set<PaymentTestOrder>().Any(o=>o.Id==x.OrderId&&o.State!="PagadaPrueba"),ct),"Ya existe una orden pendiente para esta venta. Consulta su referencia antes de volver a registrar.",409);
            order=new PaymentTestOrder{Id=input.OperacionId,Reference="TEMP-"+input.OperacionId.ToString("D"),ClienteId=input.ClienteId,
                Hash=hash,Selection=JsonSerializer.Serialize(selection,OperationProtocol.Json),Amount=selection.Count*0.20m};
            db.Add(order);
            foreach(var sale in selection.Select(x=>x.VentaId).Distinct())db.Add(new PaymentTestObligation{OrderId=order.Id,VentaId=sale});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        }
        string? providerId=null,paymentUrl=null,qrUrl=null;var registered=false;
        try {
            var name=input.NombresPagador+" "+input.ApellidosPagador;
            var result=await provider.Register(new{email_cliente=input.CorreoPagador,identificador=order.Reference,
                callback_url=(config["Libelula:CallbackBaseUrl"]??throw new ProtocolException("Falta callback público.")).TrimEnd('/')+"/api/public/pagos/callback?identificador="+Uri.EscapeDataString(order.Reference),
                url_retorno=(config["Libelula:FrontendUrl"]??throw new ProtocolException("Falta retorno público.")).TrimEnd('/')+"/pagar?txn="+Uri.EscapeDataString(order.Reference),
                descripcion="Prueba de pago; deuda real permanece pendiente",nombre_cliente=name,
                lineas_detalle_deuda=JsonSerializer.Deserialize<List<TestPaymentLine>>(order.Selection,OperationProtocol.Json)!.Select(_=>new{concepto="[PRUEBA] No liquida deuda real",costo_unitario=0.20m,cantidad=1}),
                emite_factura=0,codigo_tipo_documento="5",razon_social=name,nit=input.CiPagador},ct);
            var url=result.GetProperty("url_pasarela_pagos").GetString();
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new ProtocolException("URL Libélula no válida.");
            providerId=result.TryGetProperty("id_transaccion",out var id)?id.ToString():null;
            paymentUrl=url;qrUrl=result.TryGetProperty("qr_simple_url",out var qr)?qr.GetString():null;registered=true;
        }catch(Exception e)when(e is HttpRequestException or TaskCanceledException or ProtocolException or JsonException){ }
        // Re-read under the financial lock: an early verified callback may have already advanced the state.
        db.ChangeTracker.Clear();
        await using(var tx=await db.Database.BeginTransactionAsync(CancellationToken.None)) {
            await OperationProtocol.Lock(db,"finance:operations",CancellationToken.None);
            order=await db.Set<PaymentTestOrder>().SingleAsync(x=>x.Id==input.OperacionId);
            if(registered){order.ProviderId=providerId;order.PaymentUrl=paymentUrl;order.QrUrl=qrUrl;}
            if(order.State!="PagadaPrueba")order.State=registered?"Registrada":"RegistroIncierto";
            order.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(CancellationToken.None);await tx.CommitAsync(CancellationToken.None);
        }
        return order;
    }
    public async Task<int> Reconcile(string? reference,CancellationToken ct) {
        var query=db.Set<PaymentTestOrder>().Where(x=>x.State!="PagadaPrueba");
        if(reference is not null)query=query.Where(x=>x.Reference==reference);
        var count=0;
        foreach(var order in await query.OrderBy(x=>x.UpdatedAt).Take(10).ToListAsync(ct)) {
            try {
                if(!await provider.Verify(order.Reference,order.Currency,order.Amount,order.CreatedAt,ct))continue;
                await using var tx=await db.Database.BeginTransactionAsync(ct);await OperationProtocol.Lock(db,"finance:operations",ct);
                order.State="PagadaPrueba";order.UpdatedAt=DateTime.UtcNow;
                foreach(var item in await db.Set<PaymentTestInbox>().Where(x=>x.Reference==order.Reference).ToListAsync(ct))item.State="Conciliada";
                // The user's explicit test policy: never settle quotas/debt or create a full-value ingreso.
                await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);count++;
            }catch(Exception e)when(e is HttpRequestException or TaskCanceledException or ProtocolException or JsonException){ }
            finally {order.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(CancellationToken.None);}
        }
        return count;
    }
}
