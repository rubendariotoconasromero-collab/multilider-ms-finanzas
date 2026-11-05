using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
using MSMultilider.Finanzas.Services;
namespace MSMultilider.Finanzas.Controllers;

[ApiController,Authorize,Route("api/compat")]
public sealed class LedgerReadsController(AppDbContext db,FinanceReads reads):ControllerBase {
    private int Page=>int.TryParse(Request.Query["page"],out var value)?Math.Max(1,value):1;
    private int Per=>int.TryParse(Request.Query["per_page"],out var value)?Math.Clamp(value,1,100):15;
    private object Meta(int total)=>new{current_page=Page,last_page=Math.Max(1,(total+Per-1)/Per),per_page=Per,total};
    private IQueryable<Ingreso> Incomes() {
        var q=db.Ingresos.AsNoTracking();
        if(DateOnly.TryParse(Request.Query["fecha_inicio"],out var from))q=q.Where(x=>x.Fecha>=from);
        if(DateOnly.TryParse(Request.Query["fecha_fin"],out var until))q=q.Where(x=>x.Fecha<=until);
        if(Request.Query["categoria"].ToString() is {Length:>0} category)q=q.Where(x=>x.Categoria==category);
        if(Request.Query["origen"].ToString() is {Length:>0} origin)q=q.Where(x=>x.Origen==origin);
        if(Request.Query["estado"].ToString() is {Length:>0} state)q=q.Where(x=>x.Estado==state);
        if(Request.Query["moneda"].ToString() is {Length:>0} currency)q=q.Where(x=>x.Moneda==currency);
        if(int.TryParse(Request.Query["cuenta_bancaria_id"],out var bank))q=q.Where(x=>x.CuentaBancariaId==bank);
        if(int.TryParse(Request.Query["nota_venta_id"],out var sale))q=q.Where(x=>db.CuentasVentas.Any(c=>c.Id==x.CuentaVentaId&&c.NotaVentaId==sale));
        return q;
    }
    private IQueryable<Egreso> Expenses() {
        var q=db.Egresos.AsNoTracking();
        if(DateOnly.TryParse(Request.Query["fecha_inicio"],out var from))q=q.Where(x=>x.Fecha>=from);
        if(DateOnly.TryParse(Request.Query["fecha_fin"],out var until))q=q.Where(x=>x.Fecha<=until);
        if(Request.Query["categoria"].ToString() is {Length:>0} category)q=q.Where(x=>x.Categoria==category);
        if(Request.Query["origen"].ToString() is {Length:>0} origin)q=q.Where(x=>x.Origen==origin);
        if(Request.Query["estado"].ToString() is {Length:>0} state)q=q.Where(x=>x.Estado==state);
        if(Request.Query["moneda"].ToString() is {Length:>0} currency)q=q.Where(x=>x.Moneda==currency);
        if(int.TryParse(Request.Query["cuenta_bancaria_id"],out var bank))q=q.Where(x=>x.CuentaBancariaId==bank);
        if(int.TryParse(Request.Query["asesor_id"],out var adviser))q=q.Where(x=>x.AsesorId==adviser);
        if(int.TryParse(Request.Query["nota_venta_id"],out var sale))q=q.Where(x=>db.CuentasVentas.Any(c=>c.Id==x.CuentaVentaId&&c.NotaVentaId==sale));
        return q;
    }
    [HttpGet("ingresos"),Authorize(Policy="acceso_ingresos")]
    public async Task<IActionResult> IncomesList(CancellationToken ct) {
        var q=Incomes();var total=await q.CountAsync(ct);var rows=await q.OrderByDescending(x=>x.Fecha).ThenByDescending(x=>x.Id).Skip((Page-1)*Per).Take(Per).ToListAsync(ct);
        var data=new List<JsonObject>();foreach(var row in rows)data.Add(await reads.Income(row,ct));
        var amounts=await q.Where(x=>x.Estado=="CONFIRMADO").Select(x=>new{x.Moneda,x.Monto}).ToListAsync(ct);
        return Ok(new{data,meta=Meta(total),totales=amounts.GroupBy(x=>x.Moneda).Select(g=>new{moneda=g.Key,total=g.Sum(x=>x.Monto)})});
    }
    [HttpGet("egresos"),Authorize(Policy="acceso_egresos")]
    public async Task<IActionResult> ExpensesList(CancellationToken ct) {
        var q=Expenses();var total=await q.CountAsync(ct);var rows=await q.OrderByDescending(x=>x.Fecha).ThenByDescending(x=>x.Id).Skip((Page-1)*Per).Take(Per).ToListAsync(ct);
        var data=new List<JsonObject>();foreach(var row in rows)data.Add(await reads.Expense(row,ct));
        var amounts=await q.Where(x=>x.Estado!="ANULADO").Select(x=>new{x.Moneda,x.Estado,x.Monto}).ToListAsync(ct);
        return Ok(new{data,meta=Meta(total),totales=amounts.GroupBy(x=>new{x.Moneda,x.Estado}).Select(g=>new{moneda=g.Key.Moneda,estado=g.Key.Estado,total=g.Sum(x=>x.Monto)})});
    }
    [HttpGet("ingresos/{id:int}"),Authorize(Policy="acceso_ingresos")]
    public async Task<IActionResult> Income(int id,CancellationToken ct)=>Ok(await reads.Income(await Api.Find(db.Ingresos,id,ct),ct));
    [HttpGet("egresos/{id:int}"),Authorize(Policy="acceso_egresos")]
    public async Task<IActionResult> Expense(int id,CancellationToken ct)=>Ok(await reads.Expense(await Api.Find(db.Egresos,id,ct),ct));
    [HttpGet("ingresos/resumen"),Authorize(Policy="acceso_ingresos")]
    public async Task<IActionResult> IncomeSummary(CancellationToken ct) {
        var rows=await Incomes().Where(x=>x.Estado=="CONFIRMADO").Select(x=>new{x.Moneda,x.Monto}).ToListAsync(ct);
        return Ok(rows.GroupBy(x=>x.Moneda).Select(g=>new{moneda=g.Key,total=g.Sum(x=>x.Monto)}));
    }
    [HttpGet("egresos/resumen"),Authorize(Policy="acceso_egresos")]
    public async Task<IActionResult> ExpenseSummary(CancellationToken ct) {
        var rows=await Expenses().Where(x=>x.Estado!="ANULADO").Select(x=>new{x.Moneda,x.Estado,x.Monto}).ToListAsync(ct);
        return Ok(rows.GroupBy(x=>new{x.Moneda,x.Estado}).Select(g=>new{moneda=g.Key.Moneda,estado=g.Key.Estado,total=g.Sum(x=>x.Monto)}));
    }
    [HttpGet("comisiones-asesores"),Authorize(Policy="acceso_egresos")]
    public async Task<IActionResult> Commissions(CancellationToken ct) {
        var q=db.Egresos.AsNoTracking().Where(x=>x.Categoria=="COMISION_ASESOR"&&x.Estado!="ANULADO"&&x.AsesorId!=null);
        var search=Request.Query["search"].ToString().Trim();
        if(search.Length>0)q=q.Where(x=>db.CuentasVentas.Any(c=>c.Id==x.CuentaVentaId&&db.Set<FinancialOperation>().Any(o=>o.VentaId==c.NotaVentaId&&((o.AdviserName!=null&&o.AdviserName.Contains(search))||(o.AdviserEmail!=null&&o.AdviserEmail.Contains(search))))));
        var groups=q.GroupBy(x=>x.AsesorId).Select(g=>new{id=g.Key,count=g.Count(x=>x.Estado=="PENDIENTE"),paid=g.Count(x=>x.Estado=="PAGADO"),
            debtBob=g.Where(x=>x.Moneda=="BOB"&&x.Estado=="PENDIENTE").Sum(x=>x.Monto),debtUsd=g.Where(x=>x.Moneda=="USD"&&x.Estado=="PENDIENTE").Sum(x=>x.Monto),
            paidBob=g.Where(x=>x.Moneda=="BOB"&&x.Estado=="PAGADO").Sum(x=>x.Monto),paidUsd=g.Where(x=>x.Moneda=="USD"&&x.Estado=="PAGADO").Sum(x=>x.Monto)});
        var total=await groups.CountAsync(ct);var data=new List<object>();
        foreach(var g in await groups.OrderByDescending(x=>x.count).ThenBy(x=>x.id).Skip((Page-1)*Per).Take(Per).ToListAsync(ct)) {
            var source=await db.Set<FinancialOperation>().AsNoTracking().Where(o=>db.CuentasVentas.Any(c=>c.NotaVentaId==o.VentaId&&c.AsesorId==g.id)).OrderByDescending(o=>o.UpdatedAt).FirstOrDefaultAsync(ct);
            data.Add(new{id=g.id,nombre_completo=source?.AdviserName??$"Asesor #{g.id}",correo=source?.AdviserEmail,comisiones_pendientes=g.count,comisiones_pagadas=g.paid,total_deuda_bs=g.debtBob,total_deuda_usd=g.debtUsd,total_pagado_bs=g.paidBob,total_pagado_usd=g.paidUsd});
        }
        return Ok(new{data,meta=Meta(total)});
    }
    [HttpGet("comisiones-asesores/{id:int}/{state}"),Authorize(Policy="acceso_egresos")]
    public async Task<IActionResult> AdviserCommissions(int id,string state,CancellationToken ct) {
        Api.Require(state is "impagas" or "pagadas","Consulta no encontrada.",404);
        var status=state=="impagas"?"PENDIENTE":"PAGADO";var q=Expenses().Where(x=>x.Categoria=="COMISION_ASESOR"&&x.AsesorId==id&&x.Estado==status);
        var amounts=await q.Select(x=>new{x.Moneda,x.Monto}).ToListAsync(ct);var data=new List<JsonObject>();
        foreach(var row in await q.OrderByDescending(x=>x.Fecha).ThenByDescending(x=>x.Id).Skip((Page-1)*Per).Take(Per).ToListAsync(ct))data.Add(await reads.Expense(row,ct));
        return Ok(new{data,meta=Meta(amounts.Count),totales=new{bs=amounts.Where(x=>x.Moneda=="BOB").Sum(x=>x.Monto),usd=amounts.Where(x=>x.Moneda=="USD").Sum(x=>x.Monto)}});
    }
}
