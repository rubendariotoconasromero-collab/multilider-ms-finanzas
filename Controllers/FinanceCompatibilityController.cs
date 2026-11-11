using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
using MSMultilider.Finanzas.Services;
namespace MSMultilider.Finanzas.Controllers;
[ApiController,Authorize]
public sealed class FinanceCompatibilityController(AppDbContext db,FinanceReads reads):ControllerBase {
    [HttpGet("api/compat/{**path}")]
    public async Task<IActionResult> Get(string path,CancellationToken ct) {
        var permission=path.StartsWith("ingresos")?"acceso_ingresos":path.StartsWith("egresos")||path.StartsWith("comisiones-asesores")?"acceso_egresos":"acceso_pagos";
        var saleRead=path.StartsWith("ventas/")&&(User.HasClaim("permission","acceso_ventas")||User.HasClaim("permission","acceso_historial_ventas"));
        Api.Require(User.HasClaim("permission",permission)||saleRead,"Permiso requerido.",403);
        if(path=="ventas/lecturas") {
            var ids=Request.Query["ids"].ToString().Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>int.TryParse(x,out var id)?id:0).Distinct().ToArray();
            Api.Require(ids.Length is >0 and <=100&&ids.All(x=>x>0),"Identificadores inválidos.");
            var result=new Dictionary<string,object>();
            foreach(var account in await db.CuentasVentas.AsNoTracking().Where(x=>ids.Contains(x.NotaVentaId)).ToListAsync(ct))result[account.NotaVentaId.ToString()]=await reads.Summary(account.NotaVentaId,ct);
            return Ok(result);
        }
        var m=Regex.Match(path,@"^ventas/([1-9][0-9]*)/(plan-pago|resumen)$");
        if(m.Success)return Ok(m.Groups[2].Value=="plan-pago"?await reads.Plan(int.Parse(m.Groups[1].Value),ct):await reads.Summary(int.Parse(m.Groups[1].Value),ct));
        m=Regex.Match(path,@"^comisiones-asesores/egreso/([1-9][0-9]*)/datos-comprobante$");
        if(m.Success) {
            var expense=await Api.Find(db.Egresos,int.Parse(m.Groups[1].Value),ct);
            Api.Require(expense.Estado=="PAGADO"&&expense.Categoria=="COMISION_ASESOR","Solo comisiones desembolsadas.",422);
            var result=FinanceReads.Node(expense);
            if(expense.CuentaVentaId is int account)result["nota_venta"]=await reads.Sale(await Api.Find(db.CuentasVentas,account,ct),ct);
            if(expense.CuentaBancariaId is int bank)result["cuenta_bancaria"]=FinanceReads.Node(await Api.Find(db.CuentasBancarias,bank,ct));
            return Ok(result);
        }
        if(path=="pagos"||path=="pagos/pendientes/listar") {
            var query=db.Pagos.AsQueryable();
            var state=path.EndsWith("listar")?"PENDIENTE_PAGO":Request.Query["estado"].ToString();
            if(!string.IsNullOrEmpty(state))query=query.Where(x=>x.Estado==state);
            if(state=="PENDIENTE_PAGO")query=query.Where(x=>db.CuentasVentas.Any(c=>c.Id==x.CuentaVentaId&&c.Estado=="Activa"));
            if(int.TryParse(Request.Query["nota_venta_id"],out var venta))query=query.Where(x=>db.CuentasVentas.Any(c=>c.Id==x.CuentaVentaId&&c.NotaVentaId==venta));
            var page=int.TryParse(Request.Query["page"],out var p)?p:1;var per=int.TryParse(Request.Query["per_page"],out var n)?n:20;
            var rows=await Api.Paginate(query,page,per,ct);var data=new List<JsonObject>();foreach(var row in rows.Data)data.Add(await reads.Payment(row,ct));
            return Ok(new{data,rows.Total,rows.PageNumber,rows.PerPage,rows.LastPage});
        }
        m=Regex.Match(path,@"^pagos/([1-9][0-9]*)(/datos-comprobante)?$");
        if(m.Success) {
            var row=await Api.Find(db.Pagos,int.Parse(m.Groups[1].Value),ct);
            if(m.Groups[2].Success)Api.Require(row.Estado=="PAGADO","Solo pagos confirmados.",422);
            return Ok(await reads.Payment(row,ct));
        }
        m=Regex.Match(path,@"^pagos/cliente/([1-9][0-9]*)/resumen$");
        if(m.Success) {
            var customer=int.Parse(m.Groups[1].Value);var data=new List<object>();
            foreach(var account in await db.CuentasVentas.Where(x=>x.ClienteId==customer).ToListAsync(ct))data.Add(await reads.Summary(account.NotaVentaId,ct));
            return Ok(data);
        }
        throw new BusinessException("Consulta no disponible.",404);
    }
    [HttpGet("api/operaciones/{id:guid}")]
    public async Task<IActionResult> Command(Guid id,CancellationToken ct) {
        var actor=Api.UserId(User);
        var receipt=await db.Set<FinancialCommand>().AsNoTracking().FirstOrDefaultAsync(x=>x.OperationId==id&&x.ActorId==actor,ct)??throw new BusinessException("Recibo no encontrado.",404);
        return Ok(new{operacion_id=id,state="Completada",resultado=JsonSerializer.Deserialize<JsonElement>(receipt.Result)});
    }
}
