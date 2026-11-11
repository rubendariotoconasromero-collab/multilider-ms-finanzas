using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
using MSMultilider.Finanzas.Services;

namespace MSMultilider.Finanzas.Controllers;

[ApiController, Authorize, Route("api/lecturas")]
public sealed class AggregateReadsController(AppDbContext db, FinanceReads reads, OperationChannel channel, IConfiguration config) : ControllerBase
{
    [HttpGet("cartera")]
    public async Task<IActionResult> Accounts(bool propio = false, int page = 1, [FromQuery(Name = "asesor_id")] int? adviserId = null,
        [FromQuery(Name = "per_page")] int size = 10, CancellationToken ct = default)
    {
        var query = db.CuentasVentas.AsNoTracking().Where(x => x.Estado == "Activa");
        if (propio)
        {
            var adviser = await Profile("mi-asesor", ct);
            query = query.Where(x => x.AsesorId == adviser);
        }
        else Api.Require(User.HasClaim("permission", "acceso_pagos"), "Permiso requerido.", 403);
        if (adviserId.HasValue) query = query.Where(x => x.AsesorId == adviserId.Value);
        return Ok(await AccountPage(query.Where(x => x.TipoVenta == "CREDITO"), page, size, false, ct));
    }

    [HttpGet("mi-cartera")]
    public async Task<IActionResult> Portfolio(int page = 1, [FromQuery(Name = "per_page")] int size = 10,
        CancellationToken ct = default)
    {
        var customer = await Profile("mi-cliente", ct);
        var query = db.CuentasVentas.AsNoTracking().Where(x => x.Estado == "Activa" && x.ClienteId == customer);
        return Ok(await AccountPage(query, page, size, true, ct));
    }

    [HttpGet("mi-cartera/pagos/{id:int}")]
    public async Task<IActionResult> Receipt(int id, CancellationToken ct)
    {
        var customer = await Profile("mi-cliente", ct);
        var payment = await db.Pagos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Estado == "PAGADO" &&
            db.CuentasVentas.Any(a => a.Id == x.CuentaVentaId && a.Estado == "Activa" && a.ClienteId == customer), ct);
        if (payment is null) return NotFound();
        return Ok(await reads.Payment(payment, ct));
    }

    [HttpGet("cobros"), Authorize(Policy = "acceso_pagos")]
    public async Task<IActionResult> Payments(DateOnly? desde, DateOnly? hasta, string? ventas,
        [FromQuery(Name = "asesor_id")] int? adviser = null, [FromQuery(Name = "tipo_venta")] string? type = null,
        int page = 1, [FromQuery(Name = "per_page")] int size = 100, CancellationToken ct = default)
    {
        var query = db.Pagos.AsNoTracking().Where(x => x.Estado == "PAGADO");
        query = query.Where(x => db.CuentasVentas.Any(a => a.Id == x.CuentaVentaId && a.Estado == "Activa" &&
            (!adviser.HasValue || a.AsesorId == adviser.Value) && (type == null || a.TipoVenta == type)));
        if (desde.HasValue) query = query.Where(x => x.FechaPago >= desde.Value);
        if (hasta.HasValue) query = query.Where(x => x.FechaPago <= hasta.Value);
        if (ventas is not null)
        {
            var ids = ventas.Split(',').Select(x => int.TryParse(x, out var id) ? id : 0).Distinct().ToArray();
            Api.Require(ids.Length is > 0 and <= 100 && ids.All(x => x > 0), "Identificadores inválidos.");
            query = query.Where(x => db.CuentasVentas.Any(a => a.Id == x.CuentaVentaId && ids.Contains(a.NotaVentaId)));
        }
        var result = await Api.Paginate(query, page, size, ct);
        var accountIds = result.Data.Select(x => x.CuentaVentaId).Distinct().ToArray();
        var accounts = await db.CuentasVentas.AsNoTracking().Where(x => accountIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var data = result.Data.Select(x => new { x.Id, x.FechaPago, x.Monto, x.ConceptoPago,
            nota_venta_id = accounts[x.CuentaVentaId].NotaVentaId, accounts[x.CuentaVentaId].Moneda });
        return Ok(new { data, result.Total, result.PageNumber, result.PerPage, result.LastPage });
    }

    private async Task<object> AccountPage(IQueryable<CuentaVenta> query, int page, int size, bool payments, CancellationToken ct)
    {
        var result = await Api.Paginate(query, page, Math.Clamp(size, 1, 10), ct);
        return new { data = await reads.Accounts(result.Data, payments, ct), result.Total, result.PageNumber, result.PerPage, result.LastPage };
    }

    private async Task<int> Profile(string route, CancellationToken ct)
    {
        // Comercial owns the subject-to-profile link. The current user credential
        // travels over the authenticated channel and authorizes only that profile.
        var dataset = config["Operations:Dataset"];
        if (string.IsNullOrWhiteSpace(dataset)) throw new ProtocolException("Falta el conjunto de datos de Finanzas.", 503);
        var profile = await channel.Send("comercial", HttpMethod.Get, $"internal/lecturas/v1/{Uri.EscapeDataString(dataset)}/perfil/{route}", null, ct,
            Request.Headers.Authorization.ToString());
        return profile.GetProperty("id").GetInt32();
    }
}
