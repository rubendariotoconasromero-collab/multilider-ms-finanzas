using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.DTOs;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Services;
namespace MSMultilider.Finanzas.Controllers;

[ApiController,Route("api/ventas"),Authorize(Policy="acceso_pagos")]
public sealed class VentasController(AppDbContext db,FinanzasService service):ControllerBase {
    [HttpPost("{id:int}/registrar")] public async Task<IActionResult> Register(int id,CancellationToken ct) =>
        Ok(await service.RegistrarVenta(id,Api.UserId(User),ct));
    [HttpPost("{id:int}/anular"),Authorize(Policy="acceso_ventas")] public async Task<IActionResult> Cancel(int id,CancellationToken ct) {
        await service.AnularVenta(id,ct);return Ok(new{message="Anulación financiera registrada."});
    }
    [HttpGet("{id:int}/resumen")] public async Task<IActionResult> Summary(int id,CancellationToken ct) {
        var cuenta=await service.Cuenta(id,ct);
        var pagos=await db.Pagos.Where(x=>x.CuentaVentaId==cuenta.Id).ToListAsync(ct);
        var plan=await db.PlanesPago.SingleOrDefaultAsync(x=>x.CuentaVentaId==cuenta.Id,ct);
        var pendientes=plan is null?[]:await service.Pendientes(plan.Id,ct);
        return Ok(new{cuenta,pagos,plan_pago=plan,saldo_capital=pendientes.Sum(x=>x.MontoCapital),
            saldo_cuotas=pendientes.Sum(x=>x.MontoCuota),
            saldo_inicial=pagos.Where(x=>x.Estado=="PENDIENTE_PAGO").Sum(x=>x.Monto),
            total_pagado=pagos.Where(x=>x.Estado=="PAGADO").Sum(x=>x.Monto)});
    }
    [HttpGet("{id:int}/plan-pago")] public async Task<IActionResult> Plan(int id,CancellationToken ct) {
        var cuenta=await service.Cuenta(id,ct);
        var plan=await db.PlanesPago.SingleOrDefaultAsync(x=>x.CuentaVentaId==cuenta.Id,ct)
            ?? throw new BusinessException("La venta no tiene plan.",404);
        return Ok(new{plan_pago=plan,cuotas=await db.Cuotas.Where(x=>x.PlanPagoId==plan.Id).OrderBy(x=>x.NumeroCuota).ThenBy(x=>x.Id).ToListAsync(ct)});
    }
}
[ApiController,Route("api/planes-pago"),Authorize(Policy="acceso_pagos")]
public sealed class PlanesPagoController(AppDbContext db,FinanzasService service):ControllerBase {
    [HttpPost("{id:int}/amortizar")] public async Task<IActionResult> Amortize(int id,AmortizarRequest r,CancellationToken ct) =>
        Ok(await service.Amortizar(id,r,Api.UserId(User),ct));
    [HttpPost("{id:int}/reprogramar")] public async Task<IActionResult> Reschedule(int id,ReprogramarRequest r,CancellationToken ct) =>
        Ok(await service.Reprogramar(id,r,Api.UserId(User),ct));
    [HttpGet("{id:int}/reprogramaciones")] public async Task<IActionResult> History(int id,CancellationToken ct) {
        await Api.Find(db.PlanesPago,id,ct);
        return Ok(await db.Reprogramaciones.Where(x=>x.PlanPagoId==id).OrderBy(x=>x.Id).ToListAsync(ct));
    }
}
[ApiController,Route("api/pagos"),Authorize(Policy="acceso_pagos")]
public sealed class PagosController(AppDbContext db,FinanzasService service):ControllerBase {
    [HttpGet] public async Task<IActionResult> List(string? estado,int page=1,[FromQuery(Name="per_page")]int perPage=20,CancellationToken ct=default) {
        var query=db.Pagos.AsQueryable();if(estado is not null)query=query.Where(x=>x.Estado==estado);
        return Ok(await Api.Paginate(query,page,perPage,ct));
    }
    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id,CancellationToken ct)=>Ok(await Api.Find(db.Pagos,id,ct));
    [HttpPost] public async Task<IActionResult> Pay(PagoRequest r,CancellationToken ct)=>Ok(await service.Pagar(r,Api.UserId(User),ct));
    [HttpPost("{id:int}/procesar")] public async Task<IActionResult> Process(int id,ProcesarPagoRequest r,CancellationToken ct) {
        var pago=await Api.Find(db.Pagos,id,ct);
        Api.Require(pago.Estado is "PENDIENTE_PAGO" or "PAGADO","Este pago fue anulado.",409);
        var cuenta=await Api.Find(db.CuentasVentas,pago.CuentaVentaId,ct);
        return Ok(await service.Pagar(new PagoRequest(r.SolicitudId,cuenta.NotaVentaId,pago.Monto,r.MetodoPagoId,r.CuentaId,r.FechaPago,pago.CuotaId,r.Observaciones),Api.UserId(User),ct));
    }
    [HttpPut("{id:int}/cancelar")] public async Task<IActionResult> Cancel(int id,CancellationToken ct) {
        await service.CancelarPago(id,ct);return Ok(new{message="Pago e ingreso revertidos."});
    }
    [HttpGet("{id:int}/comprobante")] public async Task<IActionResult> Receipt(int id,CancellationToken ct) {
        var p=await Api.Find(db.Pagos,id,ct);
        var cuenta=await Api.Find(db.CuentasVentas,p.CuentaVentaId,ct);
        return File(SimplePdf.Create(["COMPROBANTE INTERNO DE PAGO",$"Pago #{p.Id} - Venta #{cuenta.NotaVentaId}",
            $"Concepto: {p.ConceptoPago}",$"Monto: {p.Monto:F2} {cuenta.Moneda}",$"Fecha: {p.FechaPago}","Estado: "+p.Estado,
            "No es una factura fiscal."]),"application/pdf",$"pago-{id}.pdf");
    }
}

