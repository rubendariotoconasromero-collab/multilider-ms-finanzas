using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.DTOs;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
using MSMultilider.Finanzas.Services;
namespace MSMultilider.Finanzas.Controllers;
public sealed record BulkQuota(int CuotaId,decimal Monto,decimal MoraMonto=0);
public sealed record BulkPayment(Guid SolicitudId,int NotaVentaId,int MetodoPagoId,int CuentaId,DateOnly FechaPago,List<BulkQuota> Cuotas,string? Observaciones=null);
[ApiController,Route("api/pagos/bulk"),Authorize(Policy="acceso_pagos")]
public sealed class BulkPaymentsController(AppDbContext db,FinanzasService service):ControllerBase {
    [HttpPost]public async Task<IActionResult> Pay(BulkPayment r,CancellationToken ct) {
        Api.Require(r.SolicitudId!=Guid.Empty&&r.Cuotas.Count is >0 and <=100&&r.Cuotas.Select(x=>x.CuotaId).Distinct().Count()==r.Cuotas.Count,"Lote inválido.");
        var pagos=new List<Pago>();var user=Api.UserId(User);
        foreach(var item in r.Cuotas) {
            var cuota=await Api.Find(db.Cuotas,item.CuotaId,ct);var plan=await Api.Find(db.PlanesPago,cuota.PlanPagoId,ct);
            var account=await Api.Find(db.CuentasVentas,plan.CuentaVentaId,ct);
            Api.Require(account.NotaVentaId==r.NotaVentaId,"Cuota de otra venta.",409);
            pagos.Add(await service.Pagar(new PagoRequest(OperationProtocol.Command(r.SolicitudId,"cuota:"+item.CuotaId),r.NotaVentaId,item.Monto,r.MetodoPagoId,r.CuentaId,r.FechaPago,item.CuotaId,r.Observaciones),user,ct));
            Api.Require(item.MoraMonto>=0,"Mora inválida.");
            if(item.MoraMonto>0)pagos.Add(await service.Pagar(new PagoRequest(OperationProtocol.Command(r.SolicitudId,"mora:"+item.CuotaId),r.NotaVentaId,item.MoraMonto,r.MetodoPagoId,r.CuentaId,r.FechaPago,item.CuotaId,r.Observaciones,"MORA"),user,ct));
        }
        return Ok(new{message="Lote confirmado.",data=pagos});
    }
}
