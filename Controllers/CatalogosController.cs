using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.DTOs;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Controllers;

[ApiController,Route("api/metodos-pago"),Authorize(Policy="acceso_pagos")]
public sealed class MetodosPagoController(AppDbContext db):ControllerBase {
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct)=>Ok(await db.MetodosPago.OrderBy(x=>x.Id).ToListAsync(ct));
    [HttpPost] public async Task<IActionResult> Create(MetodoRequest r,CancellationToken ct) {
        var row=new MetodoPago {Nombre=r.Nombre.Trim(),Estado=r.Estado};
        db.MetodosPago.Add(row);await db.SaveChangesAsync(ct);return Created($"/api/metodos-pago/{row.Id}",row);
    }
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id,MetodoRequest r,CancellationToken ct) {
        var row=await Api.Find(db.MetodosPago,id,ct);row.Nombre=r.Nombre.Trim();row.Estado=r.Estado;
        await db.SaveChangesAsync(ct);return Ok(row);
    }
}
[ApiController,Route("api/cuentas-bancarias"),Authorize(Policy="acceso_pagos")]
public sealed class CuentasBancariasController(AppDbContext db):ControllerBase {
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct)=>Ok(await db.CuentasBancarias.OrderBy(x=>x.Id).ToListAsync(ct));
    [HttpPost] public async Task<IActionResult> Create(CuentaRequest r,CancellationToken ct) {
        Api.Currency(r.Moneda);Api.Require(r.SaldoInicial==Api.Money(r.SaldoInicial),"Saldo inicial con máximo dos decimales.");
        var row=new CuentaBancaria {Nombre=r.Nombre,Banco=r.Banco,NumeroCuenta=r.NumeroCuenta,Titular=r.Titular,
            Moneda=r.Moneda,SaldoInicial=r.SaldoInicial,Estado=r.Estado};
        db.CuentasBancarias.Add(row);await db.SaveChangesAsync(ct);return Created($"/api/cuentas-bancarias/{row.Id}",row);
    }
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id,CuentaRequest r,CancellationToken ct) {
        var row=await Api.Find(db.CuentasBancarias,id,ct);
        Api.Require(row.Moneda==r.Moneda && row.SaldoInicial==r.SaldoInicial,"La moneda y el saldo inicial no se alteran después de crear la cuenta.");
        row.Nombre=r.Nombre;row.Banco=r.Banco;row.NumeroCuenta=r.NumeroCuenta;row.Titular=r.Titular;row.Estado=r.Estado;
        await db.SaveChangesAsync(ct);return Ok(row);
    }
    [HttpGet("{id:int}/saldo")] public async Task<IActionResult> Balance(int id,CancellationToken ct) {
        var cuenta=await Api.Find(db.CuentasBancarias,id,ct);
        var ingresos=(await db.Ingresos.Where(x=>x.CuentaBancariaId==id && x.Estado=="CONFIRMADO").Select(x=>x.Monto).ToListAsync(ct)).Sum();
        var egresos=(await db.Egresos.Where(x=>x.CuentaBancariaId==id && x.Estado=="PAGADO").Select(x=>x.Monto).ToListAsync(ct)).Sum();
        return Ok(new{cuenta_id=id,cuenta.Moneda,saldo=cuenta.SaldoInicial+ingresos-egresos,ingresos,egresos});
    }
}

