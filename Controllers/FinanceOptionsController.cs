using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
namespace MSMultilider.Finanzas.Controllers;
[ApiController,Route("api/finanzas/opciones"),Authorize(Policy="acceso_ventas")]
public sealed class FinanceOptionsController(AppDbContext db):ControllerBase {
    [HttpGet]public async Task<IActionResult> Get(CancellationToken ct)=>Ok(new{
        metodos_pago=await db.MetodosPago.Where(x=>x.Estado).Select(x=>new{x.Id,nombre_metodo=x.Nombre,estado="Activo"}).ToListAsync(ct),
        cuentas_bancarias=await db.CuentasBancarias.Where(x=>x.Estado).Select(x=>new{x.Id,x.Nombre,x.Moneda,x.Banco,x.NumeroCuenta,x.Titular,estado="Activa"}).ToListAsync(ct)});
}
