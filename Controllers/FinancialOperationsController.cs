using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
using MSMultilider.Finanzas.Services;
namespace MSMultilider.Finanzas.Controllers;
[ApiController,Route("internal/finanzas/v1/operaciones"),AllowAnonymous]
public sealed class FinancialOperationsController(AppDbContext db,FinancialOperations operations,OperationChannel channel,IConfiguration config):ControllerBase {
    [HttpPost("{id:guid}/preparar")]
    public async Task<IActionResult> Prepare(Guid id,PrepareFinance request,CancellationToken ct) {
        var grant=await channel.Authorize(HttpContext,id,"prepare",config);
        var snapshot=await channel.Send("comercial",HttpMethod.Get,$"internal/comercial/v1/operaciones/{id:D}/snapshot",null,ct);
        Api.Require(OperationProtocol.Hash(snapshot.GetProperty("venta"))==OperationProtocol.Hash(request.Venta),"Snapshot comercial incompatible.",409);
        var result=await operations.Prepare(grant,request,ct,snapshot.GetProperty("documento").GetRawText());
        return Ok(result);
    }
    [HttpPost("{id:guid}/activar")]
    public async Task<IActionResult> Activate(Guid id,CancellationToken ct) {
        var grant=await channel.Authorize(HttpContext,id,"activate",config);
        // Query the authority over its authenticated TLS channel; a forwarded JSON is not proof.
        var receipt=await channel.Send("propiedades",HttpMethod.Get,$"internal/catalogo/v2/operaciones/{id:D}/reserva",null,ct);
        Api.Require(receipt.GetProperty("estado_reserva").GetString()=="Confirmada" && receipt.GetProperty("propiedad_id").GetInt32()==grant.ResourceId &&
            receipt.GetProperty("operacion_id").GetGuid()==id,"Falta confirmación de Propiedades.",409);
        return Ok(await operations.Activate(id,grant.ResourceId,ct));
    }
    [HttpPost("{id:guid}/abortar-preparacion")]
    public async Task<IActionResult> Abort(Guid id,CancellationToken ct) {await operations.Abort(await channel.Authorize(HttpContext,id,"abort-finance",config),ct);return Ok(new{estado="Abortada"});}
    [HttpPost("{id:guid}/cerrar")]
    public async Task<IActionResult> Close(Guid id,CancellationToken ct)=>Ok(await operations.Close(await channel.Authorize(HttpContext,id,"close",config),ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct) {
        await channel.Authorize(HttpContext,id,"read",config);
        return Ok(await db.Set<FinancialOperation>().FindAsync([id],ct)??throw new BusinessException("Operación no encontrada.",404));
    }
    [HttpGet("{id:guid}/closure")]
    public async Task<IActionResult> Closure(Guid id,CancellationToken ct) {
        var caller=await OperationProtocol.Caller(HttpContext,config);
        Api.Require(caller is "propiedades" or "comercial","Servicio no autorizado.",403);
        var grant=await channel.Check(id,"read",ct);
        Api.Require(grant.Kind=="anulacion"&&grant.ParentOperationId.HasValue,"Cierre incompatible.",409);
        var op=await db.Set<FinancialOperation>().FindAsync([grant.ParentOperationId!.Value],ct)??throw new BusinessException("Cierre no encontrado.",404);
        Api.Require(op.State=="Cerrada"&&op.ClosedBy==id,"No hay cierre financiero terminal.",409);
        return Ok(new{operacion_id=id,venta_operacion_id=op.Id,propiedad_id=op.PropiedadId,venta_id=op.VentaId,estado="Cerrada"});
    }
    [HttpGet("{id:guid}/recibo-cierre")]
    public async Task<IActionResult> CloseReceipt(Guid id,CancellationToken ct) {
        var grant=await channel.Authorize(HttpContext,id,"read",config);
        Api.Require(grant.Kind=="anulacion" && grant.ParentOperationId.HasValue,"Cierre incompatible.",409);
        if(await db.Set<ClosureRejection>().FindAsync([id],ct) is {} denied) {
            Api.Require(denied.ParentOperationId==grant.ParentOperationId && denied.PropertyId==grant.ResourceId,"Cierre incompatible.",409);
            throw new BusinessException(denied.Reason,409);
        }
        var op=await db.Set<FinancialOperation>().FindAsync([grant.ParentOperationId!.Value],ct);
        if(op is null || op.ClosedBy!=id)throw new BusinessException("Cierre no encontrado.",404);
        Api.Require(op.State=="Cerrada" && op.PropiedadId==grant.ResourceId,"Cierre incompatible.",409);
        return Ok(op);
    }
}
