using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Controllers;
public sealed record MethodAccountRequest(int MetodoPagoId,int CuentaBancariaId);
[ApiController,Route("api/mapeo-metodos-cuentas"),Authorize(Policy="acceso_pagos")]
public sealed class MethodAccountController(AppDbContext db):ControllerBase {
    [HttpGet]public async Task<IActionResult> List(CancellationToken ct)=>Ok(await db.Set<MethodAccountDefault>().ToListAsync(ct));
    [HttpGet("{id:int}")]public async Task<IActionResult> Get(int id,CancellationToken ct)=>Ok(await Api.Find(db.Set<MethodAccountDefault>(),id,ct));
    [HttpGet("obtener-cuenta/{id:int}")]public async Task<IActionResult> Account(int id,CancellationToken ct) {
        var row=await db.Set<MethodAccountDefault>().SingleOrDefaultAsync(x=>x.MetodoPagoId==id,ct);
        return row is null?Ok(new{cuenta=(object?)null}):Ok(await Api.Find(db.CuentasBancarias,row.CuentaBancariaId,ct));
    }
    [HttpPost]public Task<IActionResult> Create(MethodAccountRequest r,CancellationToken ct)=>Save(null,r,ct);
    [HttpPut("{id:int}")]public Task<IActionResult> Update(int id,MethodAccountRequest r,CancellationToken ct)=>Save(id,r,ct);
    private async Task<IActionResult> Save(int? id,MethodAccountRequest r,CancellationToken ct) {
        var old=id.HasValue?await Api.Find(db.Set<MethodAccountDefault>(),id.Value,ct):await db.Set<MethodAccountDefault>().SingleOrDefaultAsync(x=>x.MetodoPagoId==r.MetodoPagoId,ct);
        var method=id.HasValue?old!.MetodoPagoId:r.MetodoPagoId;
        Api.Require((await Api.Find(db.MetodosPago,method,ct)).Estado&&(await Api.Find(db.CuentasBancarias,r.CuentaBancariaId,ct)).Estado,"Método o cuenta inactivos.");
        if(old is null){old=new MethodAccountDefault{MetodoPagoId=method};db.Add(old);}
        old.CuentaBancariaId=r.CuentaBancariaId;await db.SaveChangesAsync(ct);return Ok(old);
    }
    [HttpDelete("{id:int}")]public async Task<IActionResult> Delete(int id,CancellationToken ct) {
        db.Remove(await Api.Find(db.Set<MethodAccountDefault>(),id,ct));await db.SaveChangesAsync(ct);return Ok(new{message="Mapeo eliminado."});
    }
}
