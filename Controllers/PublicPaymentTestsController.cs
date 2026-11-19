using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
using MSMultilider.Finanzas.Services;
namespace MSMultilider.Finanzas.Controllers;
[ApiController,Route("api/public"),AllowAnonymous,Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-payments")]
public sealed class PublicPaymentTestsController(AppDbContext db,PaymentTests tests):ControllerBase {
    [HttpPost("pagos/procesar")]
    public async Task<IActionResult> Create(TestPaymentRequest request,CancellationToken ct) {
        var order=await tests.Create(request,ct);return StatusCode(order.PaymentUrl is null?202:200,PaymentTests.Dto(order));
    }
    [HttpGet("pagos/estado/{reference}")]
    [HttpGet("pagos/verificar/{reference}")]
    public async Task<IActionResult> Status(string reference,CancellationToken ct) {
        Api.Require(reference.Length<=80,"Referencia inválida.");
        await tests.Reconcile(reference,ct);
        return Ok(PaymentTests.Dto(await db.Set<PaymentTestOrder>().SingleOrDefaultAsync(x=>x.Reference==reference,ct)??throw new BusinessException("Orden no encontrada.",404)));
    }
    [HttpPost("pagos/confirmar-retorno")]
    public Task<IActionResult> Return(JsonElement body,CancellationToken ct)=>Status(body.GetProperty("id_transaccion").GetString()!,ct);
    [HttpGet("pagos/callback"), HttpPost("pagos/callback")]
    public async Task<IActionResult> Callback(CancellationToken ct) {
        IFormCollection? form = null;
        JsonElement body = default;
        if (HttpMethods.IsPost(Request.Method)) {
            if (Request.HasFormContentType) {
                form = await Request.ReadFormAsync(ct);
            } else if (Request.HasJsonContentType() && Request.ContentLength != 0) {
                try {
                    body = await JsonSerializer.DeserializeAsync<JsonElement>(Request.Body, cancellationToken: ct);
                } catch (JsonException) {
                    throw new BusinessException("El cuerpo de la notificación no es JSON válido.");
                }
                Api.Require(body.ValueKind == JsonValueKind.Object, "El cuerpo de la notificación debe ser un objeto.");
            }
        }

        string? Field(string name, bool queryFirst = false, bool identifier = false) {
            static string? Single(StringValues values, string field) {
                Api.Require(values.Count <= 1, $"El campo {field} debe tener un solo valor.");
                return values.FirstOrDefault();
            }
            if (queryFirst && Request.Query.TryGetValue(name, out var queryValue)) return Single(queryValue, name);
            if (form is not null && form.TryGetValue(name, out var formValue)) return Single(formValue, name);
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var value)) {
                if (value.ValueKind == JsonValueKind.Null) return null;
                Api.Require(identifier ? value.ValueKind == JsonValueKind.String
                    : value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False,
                    $"El campo {name} no es válido.");
                return value.ToString();
            }
            return Request.Query.TryGetValue(name, out var fallback) ? Single(fallback, name) : null;
        }

        var reference = Field("identificador", queryFirst: true, identifier: true)?.Trim();
        var provider = Field("transaction_id", identifier: true)?.Trim();
        reference = string.IsNullOrEmpty(reference) ? null : reference;
        provider = string.IsNullOrEmpty(provider) ? null : provider;
        Api.Require(reference is not null || provider is not null, "Indica identificador o transaction_id para la notificación.");
        Api.Require((reference?.Length ?? 0) <= 255 && (provider?.Length ?? 0) <= 255, "El identificador supera los 255 caracteres.");
        var error = Field("error") ?? "";
        var cancel = Field("cancel_order") ?? "";
        var id=OperationProtocol.Hash(new{reference,provider,error,cancel});
        await using(var tx=await db.Database.BeginTransactionAsync(ct)) {
            await OperationProtocol.Lock(db,"finance:operations",ct);
            if(!await db.Set<PaymentTestInbox>().AnyAsync(x=>x.Id==id,ct)){db.Add(new PaymentTestInbox{Id=id,Reference=reference,ProviderId=provider});await db.SaveChangesAsync(ct);}
            await tx.CommitAsync(ct);
        }
        if(reference is null&&provider is not null)reference=await db.Set<PaymentTestOrder>().Where(x=>x.ProviderId==provider).Select(x=>x.Reference).SingleOrDefaultAsync(ct);
        if(reference is not null)await tests.Reconcile(reference,ct);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new{ok=true,es_prueba=true});
    }
    [HttpPost("clientes/buscar")]
    public async Task<IActionResult> Search(JsonElement body,CancellationToken ct) {
        var ci=body.TryGetProperty("ci",out var v)?v.GetString():null;
        Api.Require(!string.IsNullOrWhiteSpace(ci)&&ci.Length<=20,"CI inválido.");
        // Bounded snapshots belong to this financial account, not a writable identity replica.
        var snapshots=await db.Set<FinancialOperation>().AsNoTracking().Where(x=>x.State=="Activa"&&x.CustomerCi==ci&&x.ReadSnapshot!="").ToListAsync(ct);
        var matches=snapshots.Where(x=>JsonSerializer.Deserialize<JsonElement>(x.ReadSnapshot).GetProperty("cliente").GetProperty("ci").GetString()==ci).ToArray();
        if(matches.Length==0)throw new BusinessException("Cliente no encontrado.",404);
        var client=JsonSerializer.Deserialize<JsonElement>(matches[0].ReadSnapshot).GetProperty("cliente");
        var pending=new List<object>();var quotas=new List<object>();
        foreach(var op in matches) {
            var account=await db.CuentasVentas.SingleAsync(x=>x.NotaVentaId==op.VentaId,ct);
            var property=JsonSerializer.Deserialize<JsonElement>(op.ReadSnapshot).GetProperty("propiedad");
            var code=property.TryGetProperty("codigo",out var c)?c.GetString():null;var type=property.TryGetProperty("tipo",out var t)?t.GetString():null;
            foreach(var p in await db.Pagos.Where(x=>x.CuentaVentaId==account.Id&&x.Estado=="PENDIENTE_PAGO"&&x.CuotaId==null&&x.Monto>0).ToListAsync(ct))
                pending.Add(new{id=p.Id,tipo=p.ConceptoPago=="VENTA_CONTADO"?"Pago al Contado":"Cuota Inicial",monto=p.Monto,moneda=account.Moneda,propiedad=code,tipo_prop=type,nota_venta_id=op.VentaId});
            var plan=await db.PlanesPago.SingleOrDefaultAsync(x=>x.CuentaVentaId==account.Id,ct);
            if(plan is not null) {
                var rows=await db.Cuotas.Where(x=>x.PlanPagoId==plan.Id&&(x.Estado=="Pendiente"||x.Estado=="Vencida")).OrderBy(x=>x.FechaVencimiento).ToListAsync(ct);
                var amounts=await InstallmentBalance.Read(db,plan,rows,ct);
                foreach(var q in rows.Where(q=>amounts[q.Id].MontoPendiente>0))
                    quotas.Add(new{cuota_id=q.Id,q.NumeroCuota,q.FechaVencimiento,monto_cuota=amounts[q.Id].MontoPendiente,moneda=account.Moneda,propiedad=code,tipo_prop=type,nota_venta_id=op.VentaId,plan_pago_id=plan.Id});
            }
        }
        return Ok(new{found=true,cliente=new{id=matches[0].Snapshot==""?0:JsonSerializer.Deserialize<JsonElement>(matches[0].Snapshot).GetProperty("cliente_id").GetInt32(),
            nombre_completo=client.GetProperty("nombre_completo").GetString(),ci},pagos_contado=pending,cuotas_pendientes=quotas});
    }
}
