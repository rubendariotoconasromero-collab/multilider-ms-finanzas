using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.DTOs;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Services;
namespace MSMultilider.Finanzas.Controllers;

[ApiController,Route("api/ingresos"),Authorize(Policy="acceso_ingresos")]
public sealed class IngresosController(AppDbContext db,FinanzasService service):ControllerBase {
    [HttpGet] public async Task<IActionResult> List(string? moneda,string? estado,int page=1,[FromQuery(Name="per_page")]int perPage=20,CancellationToken ct=default) {
        var query=db.Ingresos.AsQueryable();
        if(moneda is not null)query=query.Where(x=>x.Moneda==moneda);
        if(estado is not null)query=query.Where(x=>x.Estado==estado);
        return Ok(await Api.Paginate(query,page,perPage,ct));
    }
    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id,CancellationToken ct)=>Ok(await Api.Find(db.Ingresos,id,ct));
    [HttpPost] public async Task<IActionResult> Create(MovimientoRequest r,CancellationToken ct)=>Ok(await service.CrearIngreso(r,Api.UserId(User),ct));
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id,MovimientoRequest r,CancellationToken ct) {
        var row=await Api.Find(db.Ingresos,id,ct);
        Api.Require(row.Origen=="MANUAL" && row.Estado=="CONFIRMADO","Solo se editan ingresos manuales confirmados.",409);
        Api.Currency(r.Moneda);
        var banco=await Api.Find(db.CuentasBancarias,r.CuentaBancariaId,ct);
        Api.Require(banco.Estado && banco.Moneda==r.Moneda && r.Fecha!=default && r.Monto==Api.Money(r.Monto),"Cuenta, fecha o importe inválido.");
        Api.Require(r.Categoria is "VENTA_CONTADO" or "CUOTA_INICIAL" or "CUOTA" or "OTRO","Categoría inválida.");
        row.Fecha=r.Fecha;row.Concepto=r.Concepto;row.Categoria=r.Categoria;row.Monto=r.Monto;row.Moneda=r.Moneda;
        row.CuentaBancariaId=r.CuentaBancariaId;row.Observaciones=r.Observaciones;
        await db.SaveChangesAsync(ct);return Ok(row);
    }
    [HttpPut("{id:int}/anular")] public async Task<IActionResult> Cancel(int id,CancellationToken ct) {
        var row=await Api.Find(db.Ingresos,id,ct);
        Api.Require(row.Origen=="MANUAL","Los ingresos automáticos se revierten desde el pago.",409);
        row.Estado="ANULADO";await db.SaveChangesAsync(ct);return Ok(row);
    }
    [HttpGet("resumen")] public async Task<IActionResult> Summary(CancellationToken ct) {
        var rows=await db.Ingresos.Where(x=>x.Estado=="CONFIRMADO").ToListAsync(ct);
        return Ok(rows.GroupBy(x=>x.Moneda).Select(g=>new{moneda=g.Key,total=g.Sum(x=>x.Monto)}));
    }
}
[ApiController,Route("api/egresos"),Authorize(Policy="acceso_egresos")]
public sealed class EgresosController(AppDbContext db,FinanzasService service):ControllerBase {
    [HttpGet] public async Task<IActionResult> List(string? moneda,string? estado,int page=1,[FromQuery(Name="per_page")]int perPage=20,CancellationToken ct=default) {
        var query=db.Egresos.AsQueryable();
        if(moneda is not null)query=query.Where(x=>x.Moneda==moneda);
        if(estado is not null)query=query.Where(x=>x.Estado==estado);
        return Ok(await Api.Paginate(query,page,perPage,ct));
    }
    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id,CancellationToken ct)=>Ok(await Api.Find(db.Egresos,id,ct));
    [HttpPost] public async Task<IActionResult> Create(MovimientoRequest r,CancellationToken ct)=>Ok(await service.CrearEgreso(r,Api.UserId(User),ct));
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id,MovimientoRequest r,CancellationToken ct) {
        var row=await Api.Find(db.Egresos,id,ct);
        Api.Require(row.Origen=="MANUAL" && row.Estado=="PENDIENTE","Solo se editan egresos manuales pendientes.",409);
        Api.Currency(r.Moneda);
        var banco=await Api.Find(db.CuentasBancarias,r.CuentaBancariaId,ct);
        Api.Require(banco.Estado && banco.Moneda==r.Moneda && r.Fecha!=default && r.Monto==Api.Money(r.Monto),"Cuenta, fecha o importe inválido.");
        Api.Require(r.Categoria is "GASTO_ADMINISTRATIVO" or "GASTO_OPERATIVO" or "GASTO_MARKETING" or "PAGO_PROPIETARIO" or "OTRO","Categoría inválida.");
        row.Fecha=r.Fecha;row.Concepto=r.Concepto;row.Categoria=r.Categoria;row.Monto=r.Monto;row.Moneda=r.Moneda;
        row.CuentaBancariaId=r.CuentaBancariaId;row.Observaciones=r.Observaciones;row.Beneficiario=r.Beneficiario;
        await db.SaveChangesAsync(ct);return Ok(row);
    }
    [HttpPut("{id:int}/pagar")] public async Task<IActionResult> Pay(int id,[FromBody]DesembolsoRequest r,CancellationToken ct) {
        await service.PagarEgreso(id,r.CuentaId,Api.UserId(User),ct);return Ok(new{message="Desembolso registrado."});
    }
    [HttpPut("{id:int}/anular")] public async Task<IActionResult> Cancel(int id,CancellationToken ct) {
        var row=await Api.Find(db.Egresos,id,ct);Api.Require(row.Estado=="PENDIENTE","Solo se anulan egresos pendientes.",409);
        row.Estado="ANULADO";await db.SaveChangesAsync(ct);return Ok(row);
    }
    [HttpGet("resumen")] public async Task<IActionResult> Summary(CancellationToken ct) {
        var rows=await db.Egresos.Where(x=>x.Estado!="ANULADO").ToListAsync(ct);
        return Ok(rows.GroupBy(x=>new{x.Moneda,x.Estado}).Select(g=>new{moneda=g.Key.Moneda,estado=g.Key.Estado,total=g.Sum(x=>x.Monto)}));
    }
}
public sealed record DesembolsoRequest([System.ComponentModel.DataAnnotations.Range(1,int.MaxValue)]int CuentaId);
[ApiController,Route("api/comisiones-asesores"),Authorize(Policy="acceso_egresos")]
public sealed class ComisionesController(AppDbContext db):ControllerBase {
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) {
        var rows=await db.Egresos.Where(x=>x.Categoria=="COMISION_ASESOR" && x.Estado!="ANULADO").ToListAsync(ct);
        return Ok(rows.GroupBy(x=>new{x.AsesorId,x.Moneda}).Select(g=>new{asesor_id=g.Key.AsesorId,moneda=g.Key.Moneda,
            pendiente=g.Where(x=>x.Estado=="PENDIENTE").Sum(x=>x.Monto),pagado=g.Where(x=>x.Estado=="PAGADO").Sum(x=>x.Monto)}));
    }
    [HttpGet("{asesorId:int}/impagas")] public async Task<IActionResult> Pending(int asesorId,CancellationToken ct)=>
        Ok(await db.Egresos.Where(x=>x.Categoria=="COMISION_ASESOR" && x.AsesorId==asesorId && x.Estado=="PENDIENTE").ToListAsync(ct));
    [HttpGet("{asesorId:int}/pagadas")] public async Task<IActionResult> Paid(int asesorId,CancellationToken ct)=>
        Ok(await db.Egresos.Where(x=>x.Categoria=="COMISION_ASESOR" && x.AsesorId==asesorId && x.Estado=="PAGADO").ToListAsync(ct));
    [HttpGet("egreso/{id:int}/comprobante")] public async Task<IActionResult> Receipt(int id,CancellationToken ct) {
        var e=await Api.Find(db.Egresos,id,ct);Api.Require(e.Categoria=="COMISION_ASESOR","No es una comisión.");
        return File(SimplePdf.Create(["COMPROBANTE INTERNO DE COMISION",$"Egreso #{id} - Asesor #{e.AsesorId}",
            $"Monto: {e.Monto:F2} {e.Moneda}","Estado: "+e.Estado,"No es una factura fiscal."]),"application/pdf",$"comision-{id}.pdf");
    }
}

