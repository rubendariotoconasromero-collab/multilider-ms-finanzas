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
    [HttpGet("{id:int}")]public async Task<IActionResult> Get(int id,CancellationToken ct)=>Ok(await Api.Find(db.MetodosPago,id,ct));
    [HttpDelete("{id:int}")]public async Task<IActionResult> Delete(int id,CancellationToken ct) {
        var row=await Api.Find(db.MetodosPago,id,ct);
        Api.Require(!await db.Pagos.AnyAsync(x=>x.MetodoPagoId==id,ct)&&!await db.Set<MethodAccountDefault>().AnyAsync(x=>x.MetodoPagoId==id,ct),"Método utilizado; desactívalo para conservar el historial.",409);
        db.Remove(row);await db.SaveChangesAsync(ct);return Ok(new{message="Método eliminado."});
    }
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
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct)=>Ok(await db.CuentasBancarias.Where(x=>x.Estado).OrderBy(x=>x.Id).ToListAsync(ct));
    [HttpGet("{id:int}")]public async Task<IActionResult> Get(int id,CancellationToken ct)=>Ok(await Api.Find(db.CuentasBancarias,id,ct));
    [HttpDelete("{id:int}")]public async Task<IActionResult> Delete(int id,CancellationToken ct) {
        var row=await Api.Find(db.CuentasBancarias,id,ct);
        Api.Require(!await db.Ingresos.AnyAsync(x=>x.CuentaBancariaId==id,ct)&&!await db.Egresos.AnyAsync(x=>x.CuentaBancariaId==id,ct)&&!await db.Pagos.AnyAsync(x=>x.CuentaId==id,ct)&&!await db.Set<MethodAccountDefault>().AnyAsync(x=>x.CuentaBancariaId==id,ct),"Cuenta utilizada; desactívala para conservar el historial.",409);
        db.Remove(row);await db.SaveChangesAsync(ct);return Ok(new{message="Cuenta eliminada."});
    }
    [HttpPost] public async Task<IActionResult> Create(CuentaRequest r,CancellationToken ct) {
        Validate(r);Api.Require(r.SaldoInicial==Api.Money(r.SaldoInicial),"Saldo inicial con máximo dos decimales.");
        var row=new CuentaBancaria {Nombre=r.Nombre,Banco=r.Banco??"",NumeroCuenta=r.NumeroCuenta??"",Titular=r.Titular??"",
            Tipo=r.Tipo,Descripcion=r.Descripcion,Iban=r.Iban,Proveedor=r.Proveedor,CodigoIntegracion=r.CodigoIntegracion,
            Moneda=r.Moneda,SaldoInicial=r.SaldoInicial,Estado=r.Estado};
        db.CuentasBancarias.Add(row);await db.SaveChangesAsync(ct);return Created($"/api/cuentas-bancarias/{row.Id}",row);
    }
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id,CuentaRequest r,CancellationToken ct) {
        var row=await Api.Find(db.CuentasBancarias,id,ct);
        Validate(r);
        var used=await db.Ingresos.AnyAsync(x=>x.CuentaBancariaId==id,ct)||await db.Egresos.AnyAsync(x=>x.CuentaBancariaId==id,ct)||await db.Pagos.AnyAsync(x=>x.CuentaId==id,ct);
        Api.Require(!used||row.Moneda==r.Moneda&&row.SaldoInicial==r.SaldoInicial,"Una cuenta con movimientos conserva moneda y saldo inicial.",409);
        row.Moneda=r.Moneda;row.SaldoInicial=r.SaldoInicial;row.Tipo=r.Tipo;row.Descripcion=r.Descripcion;row.Iban=r.Iban;row.Proveedor=r.Proveedor;row.CodigoIntegracion=r.CodigoIntegracion;
        row.Nombre=r.Nombre;row.Banco=r.Banco??"";row.NumeroCuenta=r.NumeroCuenta??"";row.Titular=r.Titular??"";row.Estado=r.Estado;
        await db.SaveChangesAsync(ct);return Ok(row);
    }
    private static void Validate(CuentaRequest r) {Api.Currency(r.Moneda);Api.Require(r.Tipo is "EFECTIVO" or "BANCARIA" or "DIGITAL" or "OTRA","Tipo de cuenta inválido.");Api.Require(r.SaldoInicial>=0&&r.SaldoInicial==Api.Money(r.SaldoInicial),"Saldo inicial inválido.");}
    [HttpGet("{id:int}/saldo")] public async Task<IActionResult> Balance(int id,CancellationToken ct) {
        var cuenta=await Api.Find(db.CuentasBancarias,id,ct);
        var ingresos=(await db.Ingresos.Where(x=>x.CuentaBancariaId==id && x.Estado=="CONFIRMADO").Select(x=>x.Monto).ToListAsync(ct)).Sum();
        var egresos=(await db.Egresos.Where(x=>x.CuentaBancariaId==id && x.Estado=="PAGADO").Select(x=>x.Monto).ToListAsync(ct)).Sum();
        return Ok(new{cuenta_id=id,cuenta.Moneda,saldo=cuenta.SaldoInicial+ingresos-egresos,ingresos,egresos});
    }
}

