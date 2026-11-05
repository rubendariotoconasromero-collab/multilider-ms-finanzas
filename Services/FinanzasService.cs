using System.Data;
using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.DTOs;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
namespace MSMultilider.Finanzas.Services;

public sealed class ComercialClient(HttpClient client,IHttpContextAccessor accessor):RelatedApi(client,accessor);
public sealed class FinanzasService(AppDbContext db,ComercialClient comercial) {
    public async Task<CuentaVenta> RegistrarVenta(int id,int usuario,CancellationToken ct) {
        var snapshot=await comercial.Get<VentaFinanciera>($"api/ventas/{id}/financiacion",ct);
        Api.Require(snapshot.Id==id && snapshot.Estado=="Activa","La venta no está activa.",409);
        Api.Currency(snapshot.Moneda);
        Api.Require(snapshot.MontoLiquido>0 && snapshot.MontoLiquido==Api.Money(snapshot.MontoLiquido) &&
            snapshot.MontoComision>=0 && snapshot.ClienteId>0 && snapshot.AsesorId>0,"Venta financiera inválida.");
        Api.Require(snapshot.TipoVenta is "CONTADO" or "CREDITO","Tipo de venta inválido.");
        if(snapshot.TipoVenta=="CREDITO")
            Api.Require(snapshot.CuotaInicial>=0 && snapshot.SaldoCredito==snapshot.MontoLiquido-snapshot.CuotaInicial &&
                snapshot.SaldoCredito>0 && snapshot.NumeroCuotas is >=1 and <=600 && snapshot.FechaInicioPago.HasValue,
                "Las condiciones de crédito no coinciden con el importe de venta.");
        await using var tx=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        Api.Require(!await db.VentasAnuladas.AnyAsync(x=>x.NotaVentaId==id,ct),"La venta ya fue anulada en Finanzas.",409);
        var cuenta=await db.CuentasVentas.SingleOrDefaultAsync(x=>x.NotaVentaId==id,ct);
        if(cuenta is not null) return cuenta; // Registration is idempotent, never reset paid installments.
        cuenta=new CuentaVenta {NotaVentaId=id,ClienteId=snapshot.ClienteId,AsesorId=snapshot.AsesorId,
            TipoVenta=snapshot.TipoVenta,Moneda=snapshot.Moneda,MontoLiquido=snapshot.MontoLiquido,
            CuotaInicial=snapshot.CuotaInicial,MontoComision=snapshot.MontoComision};
        db.CuentasVentas.Add(cuenta);await db.SaveChangesAsync(ct);
        if(snapshot.TipoVenta=="CREDITO") {
            var plan=new PlanPago {CuentaVentaId=cuenta.Id,Monto=snapshot.SaldoCredito,NumeroCuotas=snapshot.NumeroCuotas,
                TasaInteres=snapshot.TasaInteres,TasaMora=snapshot.TasaMora,FechaInicio=snapshot.FechaInicioPago!.Value,
                FechaFinal=snapshot.FechaInicioPago.Value.AddMonths(snapshot.NumeroCuotas-1)};
            db.PlanesPago.Add(plan);await db.SaveChangesAsync(ct);
            var cuotas=Amortizacion.Generar(plan.Monto,plan.NumeroCuotas,plan.TasaInteres,plan.FechaInicio);
            cuotas.ForEach(x=>x.PlanPagoId=plan.Id);db.Cuotas.AddRange(cuotas);
        }
        var inicial=snapshot.TipoVenta=="CONTADO"?snapshot.MontoLiquido:snapshot.CuotaInicial;
        if(inicial>0) db.Pagos.Add(new Pago {CuentaVentaId=cuenta.Id,Monto=inicial,
            ConceptoPago=snapshot.TipoVenta=="CONTADO"?"VENTA_CONTADO":"CUOTA_INICIAL"});
        if(snapshot.MontoComision>0) db.Egresos.Add(new Egreso {CuentaVentaId=cuenta.Id,AsesorId=cuenta.AsesorId,
            Fecha=snapshot.Fecha,Concepto=$"Comisión venta #{id}",Categoria="COMISION_ASESOR",Origen="AUTOMATICO",
            Monto=snapshot.MontoComision,Moneda=cuenta.Moneda,UserId=usuario});
        await db.SaveChangesAsync(ct);if(tx is not null)await tx.CommitAsync(ct);return cuenta;
    }
    public async Task AnularVenta(int id,CancellationToken ct) {
        await using var tx=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        var cuenta=await db.CuentasVentas.SingleOrDefaultAsync(x=>x.NotaVentaId==id,ct);
        if(cuenta is not null) {
            Api.Require(!await db.Pagos.AnyAsync(x=>x.CuentaVentaId==cuenta.Id && x.Estado=="PAGADO",ct),
                "Primero revierte los pagos de la venta.",409);
            Api.Require(!await db.Egresos.AnyAsync(x=>x.CuentaVentaId==cuenta.Id && x.Estado=="PAGADO",ct),
                "La venta tiene comisiones desembolsadas.",409);
            cuenta.Estado="Anulada";
            foreach(var p in await db.Pagos.Where(x=>x.CuentaVentaId==cuenta.Id && x.Estado=="PENDIENTE_PAGO").ToListAsync(ct)) p.Estado="ANULADO";
            foreach(var e in await db.Egresos.Where(x=>x.CuentaVentaId==cuenta.Id && x.Estado=="PENDIENTE").ToListAsync(ct)) e.Estado="ANULADO";
            var plan=await db.PlanesPago.SingleOrDefaultAsync(x=>x.CuentaVentaId==cuenta.Id,ct);
            if(plan is not null)
                foreach(var c in await db.Cuotas.Where(x=>x.PlanPagoId==plan.Id && x.Estado=="Pendiente").ToListAsync(ct)) c.Estado="Anulada";
        }
        if(!await db.VentasAnuladas.AnyAsync(x=>x.NotaVentaId==id,ct)) db.VentasAnuladas.Add(new VentaAnulada {NotaVentaId=id});
        await db.SaveChangesAsync(ct);if(tx is not null)await tx.CommitAsync(ct);
    }
    public async Task<CuentaVenta> Cuenta(int ventaId,CancellationToken ct) =>
        await db.CuentasVentas.SingleOrDefaultAsync(x=>x.NotaVentaId==ventaId,ct) ?? throw new BusinessException("Registra primero la venta en Finanzas.",404);

    private async Task CuentaValida(int cuentaId,string moneda,CancellationToken ct) {
        var banco=await Api.Find(db.CuentasBancarias,cuentaId,ct);
        Api.Require(banco.Estado && banco.Moneda==moneda,"Cuenta bancaria inactiva o moneda diferente.");
    }
    private async Task FormaPago(int metodo,int cuenta,string moneda,CancellationToken ct) {
        Api.Require((await Api.Find(db.MetodosPago,metodo,ct)).Estado,"Método de pago inactivo.");
        await CuentaValida(cuenta,moneda,ct);
    }
    private static void NuevaOperacion(Guid id,decimal monto,DateOnly fecha) {
        Api.Require(id!=Guid.Empty,"SolicitudId debe ser un UUID.");
        Api.Require(monto>0 && monto==Api.Money(monto),"El monto debe ser positivo y tener máximo dos decimales.");
        Api.Require(fecha!=default,"Fecha de pago requerida.");
    }
    private void IngresoPago(Pago p,CuentaVenta c,int usuario) => db.Ingresos.Add(new Ingreso {
        PagoId=p.Id,CuentaVentaId=c.Id,CuentaBancariaId=p.CuentaId!.Value,UserId=usuario,Fecha=p.FechaPago!.Value,
        Monto=p.Monto,Moneda=c.Moneda,Concepto=$"{p.ConceptoPago} - Venta #{c.NotaVentaId}",Categoria=p.ConceptoPago,
        Origen="AUTOMATICO",Observaciones=p.Observaciones});
    private static void Idempotente(Pago p,int cuentaId,Guid solicitud,decimal monto,int metodo,int banco,DateOnly fecha,string concepto,int? cuota) {
        Api.Require(p.CuentaVentaId==cuentaId && p.SolicitudId==solicitud && p.Monto==monto && p.MetodoPagoId==metodo
            && p.CuentaId==banco && p.FechaPago==fecha && p.ConceptoPago==concepto && p.CuotaId==cuota,
            "SolicitudId ya se utilizó con otros datos.",409);
    }
    public async Task<Pago> Pagar(PagoRequest r,int usuario,CancellationToken ct,Pago? pendingTarget=null) {
        NuevaOperacion(r.SolicitudId,r.Monto,r.FechaPago);
        await using var tx=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        var cuenta=await Cuenta(r.NotaVentaId,ct);
        var concepto=r.ConceptoPago??(r.CuotaId.HasValue?"CUOTA":cuenta.TipoVenta=="CONTADO"?"VENTA_CONTADO":"CUOTA_INICIAL");
        Api.Require(concepto is "CUOTA" or "CUOTA_INICIAL" or "VENTA_CONTADO" or "MORA" or "OTRO","Concepto inválido.");
        var previous=await db.Pagos.SingleOrDefaultAsync(x=>x.SolicitudId==r.SolicitudId,ct);
        if(previous is not null) {
            Api.Require(previous.UsuarioId==usuario,"El comando pertenece a otro actor.",403);
            Idempotente(previous,cuenta.Id,r.SolicitudId,r.Monto,r.MetodoPagoId,r.CuentaId,r.FechaPago,concepto,r.CuotaId);return previous;
        }
        Api.Require(cuenta.Estado=="Activa","Venta anulada.",409);
        await FormaPago(r.MetodoPagoId,r.CuentaId,cuenta.Moneda,ct);
        Pago? pago=null;
        if(r.CuotaId.HasValue) {
            var plan=await db.PlanesPago.SingleOrDefaultAsync(x=>x.CuentaVentaId==cuenta.Id,ct)
                ?? throw new BusinessException("La venta no tiene plan de pagos.");
            var cuota=await Api.Find(db.Cuotas,r.CuotaId.Value,ct);
            Api.Require(cuota.PlanPagoId==plan.Id && (cuota.Estado is "Pendiente" or "Vencida" || concepto=="MORA" && cuota.Estado=="Pagada"),"Cuota ajena a la venta, pagada o reemplazada.",409);
            Api.Require(concepto is "CUOTA" or "MORA","Concepto incompatible con cuota.");
            var pagado=await db.Pagos.Where(x=>x.CuotaId==cuota.Id&&x.Estado=="PAGADO"&&x.ConceptoPago==concepto).Select(x=>x.Monto).ToListAsync(ct);
            var exigible=concepto=="MORA"?Api.Money(cuota.MontoCuota*plan.TasaMora/100m/30m*Math.Max(0,r.FechaPago.DayNumber-cuota.FechaVencimiento.DayNumber)):cuota.MontoCuota;
            Api.Require(r.Monto<=exigible-pagado.Sum(),"El monto excede el saldo exigible.");
            if(concepto=="CUOTA"&&r.Monto+pagado.Sum()==cuota.MontoCuota)cuota.Estado="Pagada";
        } else if(concepto!="OTRO") {
            Api.Require(concepto==(cuenta.TipoVenta=="CONTADO"?"VENTA_CONTADO":"CUOTA_INICIAL"),"Concepto incompatible con la venta.");
            pago=await db.Pagos.SingleOrDefaultAsync(x=>x.CuentaVentaId==cuenta.Id && x.ConceptoPago==concepto && x.Estado=="PENDIENTE_PAGO",ct);
            Api.Require(pago is not null && pago.Monto>=r.Monto,"El monto excede el pago pendiente.",409);
            if(pago!.Monto>r.Monto) {
                pago.Monto-=r.Monto;
                pago=null; // A partial receipt is independent; the remaining obligation stays pending.
            }
        }
        if(pendingTarget is not null) {
            Api.Require(pendingTarget.Estado=="PENDIENTE_PAGO"&&pendingTarget.CuentaVentaId==cuenta.Id&&pendingTarget.CuotaId==r.CuotaId&&pendingTarget.ConceptoPago==concepto&&pendingTarget.Monto==r.Monto&&(pago is null||pago.Id==pendingTarget.Id),"Obligación pendiente incompatible.",409);
            pago=pendingTarget;
        }
        if(pago is null) {pago=new Pago {CuentaVentaId=cuenta.Id,CuotaId=r.CuotaId,ConceptoPago=concepto,Monto=r.Monto};db.Pagos.Add(pago);}
        pago.SolicitudId=r.SolicitudId;pago.CuentaId=r.CuentaId;pago.MetodoPagoId=r.MetodoPagoId;pago.FechaPago=r.FechaPago;
        pago.Estado="PAGADO";pago.UsuarioId=usuario;pago.Observaciones=r.Observaciones;
        cuenta.UpdatedAt=DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        IngresoPago(pago,cuenta,usuario);
        await db.SaveChangesAsync(ct);if(tx is not null)await tx.CommitAsync(ct);return pago;
    }
    public async Task CancelarPago(int id,CancellationToken ct) {
        await using var tx=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        var pago=await Api.Find(db.Pagos,id,ct);
        if(pago.Estado=="ANULADO") return;
        Api.Require(pago.Estado=="PAGADO" && pago.ConceptoPago!="AMORTIZACION","Solo se revierte un pago confirmado ordinario.",409);
        var cuenta=await Api.Find(db.CuentasVentas,pago.CuentaVentaId,ct);
        Api.Require(cuenta.Estado=="Activa","Venta anulada.",409);
        if(!await Coordinated(cuenta,ct))Api.Require(!await db.Pagos.AnyAsync(x=>x.CuentaVentaId==cuenta.Id && x.Estado=="PAGADO" && x.Id>pago.Id,ct),"Revierte primero los pagos posteriores.",409);
        if(pago.CuotaId is int quotaId)Api.Require((await Api.Find(db.Cuotas,quotaId,ct)).Estado!="Reprogramada","La cuota pertenece a una revisión histórica.",409);
        var plan=await db.PlanesPago.SingleOrDefaultAsync(x=>x.CuentaVentaId==cuenta.Id,ct);
        if(plan is not null) {
            Api.Require(!await db.Reprogramaciones.AnyAsync(x=>x.PlanPagoId==plan.Id && x.CreatedAt>pago.UpdatedAt,ct) &&
                !await db.Pagos.AnyAsync(x=>x.CuentaVentaId==cuenta.Id && x.Estado=="PAGADO" && x.ConceptoPago=="AMORTIZACION" && x.CreatedAt>pago.UpdatedAt,ct),
                "No se revierte un pago anterior a una reprogramación o amortización.",409);
        }
        pago.Estado="ANULADO";
        var ingreso=await db.Ingresos.SingleAsync(x=>x.PagoId==id,ct);ingreso.Estado="ANULADO";
        if(pago.CuotaId.HasValue && pago.ConceptoPago=="CUOTA") (await Api.Find(db.Cuotas,pago.CuotaId.Value,ct)).Estado="Pendiente";
        else if(pago.ConceptoPago is "VENTA_CONTADO" or "CUOTA_INICIAL") {
            var pending=await db.Pagos.SingleOrDefaultAsync(x=>x.CuentaVentaId==cuenta.Id&&x.ConceptoPago==pago.ConceptoPago&&x.Estado=="PENDIENTE_PAGO",ct);
            if(pending is null)db.Pagos.Add(new Pago {CuentaVentaId=cuenta.Id,ConceptoPago=pago.ConceptoPago,Monto=pago.Monto});
            else pending.Monto+=pago.Monto;
        }
        cuenta.UpdatedAt=DateTime.UtcNow;
        await db.SaveChangesAsync(ct);if(tx is not null)await tx.CommitAsync(ct);
    }
    public async Task<Pago> Amortizar(int planId,AmortizarRequest r,int usuario,CancellationToken ct) {
        NuevaOperacion(r.SolicitudId,r.Monto,r.FechaPago);
        await using var tx=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        var plan=await Api.Find(db.PlanesPago,planId,ct);
        var cuenta=await Api.Find(db.CuentasVentas,plan.CuentaVentaId,ct);
        var previous=await db.Pagos.SingleOrDefaultAsync(x=>x.SolicitudId==r.SolicitudId,ct);
        if(previous is not null) {Api.Require(previous.UsuarioId==usuario,"El comando pertenece a otro actor.",403);Idempotente(previous,cuenta.Id,r.SolicitudId,r.Monto,r.MetodoPagoId,r.CuentaId,r.FechaPago,"AMORTIZACION",null);return previous;}
        Api.Require(cuenta.Estado=="Activa","Venta anulada.",409);
        if(!await Coordinated(cuenta,ct))Api.Require(!await db.Pagos.AnyAsync(x=>x.CuentaVentaId==cuenta.Id && x.ConceptoPago=="CUOTA_INICIAL" && x.Estado=="PENDIENTE_PAGO"&&x.Monto>0,ct),"Confirma primero la cuota inicial.");
        await FormaPago(r.MetodoPagoId,r.CuentaId,cuenta.Moneda,ct);
        var cuotas=await Pendientes(plan.Id,ct);
        var balances=await InstallmentBalance.Read(db,plan,cuotas,ct);
        var saldo=balances.Values.Sum(x=>x.CapitalPendiente);
        Api.Require(r.Monto<saldo,"La amortización debe ser menor al saldo capital pendiente.");
        var nuevas=await Schedule(cuenta,saldo-r.Monto,cuotas.Count,plan.TasaInteres,cuotas[0].FechaVencimiento,1,ct);
        for(var i=0;i<cuotas.Count;i++) {
            var current=cuotas[i];var replacement=nuevas[i];
            replacement.InteresAcreditado=Api.Money(current.InteresAcreditado+balances[current.Id].InteresPagado);
            if(replacement.InteresAcreditado>0) {
                replacement.MontoInteres=Api.Money(Math.Max(0,replacement.MontoInteres-replacement.InteresAcreditado));
                replacement.MontoCuota=replacement.MontoCapital+replacement.MontoInteres;
            }
            if(balances[current.Id].MontoPagado>0) {
                replacement.PlanPagoId=plan.Id;replacement.NumeroCuota=current.NumeroCuota;replacement.FechaVencimiento=current.FechaVencimiento;
                replacement.ReprogramacionId=current.ReprogramacionId;replacement.Estado=current.Estado;current.Estado="Reprogramada";db.Cuotas.Add(replacement);
            } else {
                current.MontoCapital=replacement.MontoCapital;current.MontoInteres=replacement.MontoInteres;
                current.MontoCuota=replacement.MontoCuota;current.SaldoCapital=replacement.SaldoCapital;
                current.InteresAcreditado=replacement.InteresAcreditado;
            }
        }
        var pago=new Pago {SolicitudId=r.SolicitudId,CuentaVentaId=cuenta.Id,Monto=r.Monto,ConceptoPago="AMORTIZACION",
            CuentaId=r.CuentaId,MetodoPagoId=r.MetodoPagoId,FechaPago=r.FechaPago,Estado="PAGADO",UsuarioId=usuario,Observaciones=r.Observaciones};
        db.Pagos.Add(pago);cuenta.UpdatedAt=DateTime.UtcNow;
        await db.SaveChangesAsync(ct);IngresoPago(pago,cuenta,usuario);
        await db.SaveChangesAsync(ct);if(tx is not null)await tx.CommitAsync(ct);return pago;
    }
    public async Task<Reprogramacion> Reprogramar(int id,ReprogramarRequest r,int usuario,CancellationToken ct) {
        Api.Require(r.NuevaTasaInteres==Api.Money(r.NuevaTasaInteres),"La tasa admite máximo dos decimales.");
        Api.Require(r.SolicitudId!=Guid.Empty && r.FechaReprogramacion!=default && r.NuevaFechaInicio>=r.FechaReprogramacion,"Solicitud o fechas inválidas.");
        await using var tx=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        var previous=await db.Reprogramaciones.SingleOrDefaultAsync(x=>x.SolicitudId==r.SolicitudId,ct);
        if(previous is not null) {
            Api.Require(previous.UsuarioId==usuario&&previous.PlanPagoId==id && previous.CuotaDesde==r.CuotaDesde && previous.NuevaTasaInteres==r.NuevaTasaInteres &&
                previous.NuevoNumeroCuotas==r.NuevoNumeroCuotas && previous.NuevaFechaInicio==r.NuevaFechaInicio &&
                previous.Motivo==r.Motivo && previous.FechaReprogramacion==r.FechaReprogramacion,"SolicitudId usado con otros datos.",409);
            return previous;
        }
        var plan=await Api.Find(db.PlanesPago,id,ct);
        var cuenta=await Api.Find(db.CuentasVentas,plan.CuentaVentaId,ct);
        Api.Require(cuenta.Estado=="Activa","Venta anulada.",409);
        var allPending=await Pendientes(id,ct);
        var coordinated=await Coordinated(cuenta,ct);
        Api.Require(coordinated||allPending.Count>0&&allPending[0].NumeroCuota==r.CuotaDesde,"Reprograma desde la primera cuota pendiente.");
        var pendientes=allPending.Where(x=>x.NumeroCuota>=r.CuotaDesde).ToList();
        var saldo=(await InstallmentBalance.Read(db,plan,pendientes,ct)).Values.Sum(x=>x.CapitalPendiente);
        Api.Require(pendientes.Count>0&&saldo>0,"No hay capital pendiente en el tramo seleccionado.");
        var registro=new Reprogramacion {PlanPagoId=id,UsuarioId=usuario,SolicitudId=r.SolicitudId,Motivo=r.Motivo,
            FechaReprogramacion=r.FechaReprogramacion,CuotaDesde=r.CuotaDesde,SaldoCapitalMomento=saldo,
            NuevaTasaInteres=r.NuevaTasaInteres,NuevoNumeroCuotas=r.NuevoNumeroCuotas,NuevaFechaInicio=r.NuevaFechaInicio};
        db.Reprogramaciones.Add(registro);
        pendientes.ForEach(x=>x.Estado="Reprogramada");
        await db.SaveChangesAsync(ct);
        var nuevas=await Schedule(cuenta,saldo,r.NuevoNumeroCuotas,r.NuevaTasaInteres,r.NuevaFechaInicio,r.CuotaDesde,ct);
        var used=await db.Cuotas.Where(x=>x.PlanPagoId==id&&x.Estado!="Reprogramada"&&x.Estado!="Anulada").Select(x=>x.NumeroCuota).ToListAsync(ct);
        var number=r.CuotaDesde;foreach(var quota in nuevas){while(used.Contains(number))number++;quota.NumeroCuota=number++;quota.PlanPagoId=id;quota.ReprogramacionId=registro.Id;}
        db.Cuotas.AddRange(nuevas);
        plan.NumeroCuotas=used.Count+r.NuevoNumeroCuotas;plan.TasaInteres=r.NuevaTasaInteres;
        plan.FechaFinal=coordinated?Amortizacion.MonthOverflow(r.NuevaFechaInicio,r.NuevoNumeroCuotas-1):r.NuevaFechaInicio.AddMonths(r.NuevoNumeroCuotas-1);cuenta.UpdatedAt=DateTime.UtcNow;
        await db.SaveChangesAsync(ct);if(tx is not null)await tx.CommitAsync(ct);return registro;
    }
    public Task<List<Cuota>> Pendientes(int plan,CancellationToken ct) =>
        db.Cuotas.Where(x=>x.PlanPagoId==plan && (x.Estado=="Pendiente"||x.Estado=="Vencida")).OrderBy(x=>x.NumeroCuota).ToListAsync(ct);
    private Task<bool> Coordinated(CuentaVenta account,CancellationToken ct)=>db.Set<FinancialOperation>().AnyAsync(x=>x.VentaId==account.NotaVentaId,ct);
    private async Task<List<Cuota>> Schedule(CuentaVenta account,decimal capital,int months,decimal rate,DateOnly start,int from,CancellationToken ct) {
        var op=await db.Set<FinancialOperation>().AsNoTracking().SingleOrDefaultAsync(x=>x.VentaId==account.NotaVentaId,ct);
        return op is not null&&System.Text.Json.JsonSerializer.Deserialize<VentaFinanciera>(op.Snapshot,Multilider.Operations.OperationProtocol.Json)?.CalculationVersion=="laravel-v1"
            ?Amortizacion.GenerarLaravel(capital,months,rate,start,from):Amortizacion.Generar(capital,months,rate,start,from);
    }
    public async Task<Ingreso> CrearIngreso(MovimientoRequest r,int usuario,CancellationToken ct) {
        await ValidarMovimiento(r,ct);
        Api.Require(r.Categoria is "VENTA_CONTADO" or "CUOTA_INICIAL" or "CUOTA" or "OTRO","Categoría inválida.");
        var row=new Ingreso {Fecha=r.Fecha,Concepto=r.Concepto,Categoria=r.Categoria,Monto=r.Monto,Moneda=r.Moneda,
            CuentaBancariaId=r.CuentaBancariaId,UserId=usuario,Observaciones=r.Observaciones,Comprobante=r.Comprobante,CuentaVentaId=await ManualAccount(r.NotaVentaId,ct)};
        db.Ingresos.Add(row);await db.SaveChangesAsync(ct);return row;
    }
    public async Task<Egreso> CrearEgreso(MovimientoRequest r,int usuario,CancellationToken ct) {
        await ValidarMovimiento(r,ct);
        Api.Require(r.Categoria is "GASTO_ADMINISTRATIVO" or "GASTO_OPERATIVO" or "GASTO_MARKETING" or "PAGO_PROPIETARIO" or "OTRO","Las comisiones se generan desde las ventas.");
        var row=new Egreso {Fecha=r.Fecha,Concepto=r.Concepto,Categoria=r.Categoria,Monto=r.Monto,Moneda=r.Moneda,
            CuentaBancariaId=r.CuentaBancariaId,UserId=usuario,Observaciones=r.Observaciones,Beneficiario=r.Beneficiario,Comprobante=r.Comprobante,CuentaVentaId=await ManualAccount(r.NotaVentaId,ct)};
        Api.Require(r.Estado is "PENDIENTE" or "PAGADO","Estado inválido.");row.Estado=r.Estado;
        db.Egresos.Add(row);await db.SaveChangesAsync(ct);return row;
    }
    private async Task ValidarMovimiento(MovimientoRequest r,CancellationToken ct) {
        Api.Currency(r.Moneda);Api.Require(r.Fecha!=default && r.Monto==Api.Money(r.Monto),"Fecha o precisión del importe inválida.");
        if(r.CuentaBancariaId is int bank)await CuentaValida(bank,r.Moneda,ct);
    }
    private async Task<int?> ManualAccount(int? sale,CancellationToken ct) {
        if(sale is null)return null;var account=await Cuenta(sale.Value,ct);Api.Require(account.Estado=="Activa","Venta no activa.",409);return account.Id;
    }
    public async Task PagarEgreso(int id,int? cuentaId,int usuario,CancellationToken ct,DateOnly? fecha=null,string? observaciones=null,string? comprobante=null) {
        await using var tx=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        var e=await Api.Find(db.Egresos,id,ct);
        if(e.Estado=="PAGADO") {Api.Require(e.CuentaBancariaId==cuentaId&&(!fecha.HasValue||e.Fecha==fecha)&&e.Observaciones==observaciones&&e.Comprobante==comprobante,"El egreso ya tiene otro desembolso registrado.",409);return;}
        Api.Require(e.Estado=="PENDIENTE","Egreso anulado.",409);
        if(cuentaId is int bank)await CuentaValida(bank,e.Moneda,ct);
        if(e.CuentaVentaId.HasValue) {
            var cuenta=await Api.Find(db.CuentasVentas,e.CuentaVentaId.Value,ct);
            Api.Require(cuenta.Estado=="Activa","Venta anulada.",409);
            cuenta.UpdatedAt=DateTime.UtcNow;
        }
        e.Estado="PAGADO";e.CuentaBancariaId=cuentaId;e.UserId=usuario;if(fecha.HasValue)e.Fecha=fecha.Value;e.Observaciones=observaciones;e.Comprobante=comprobante;
        await db.SaveChangesAsync(ct);if(tx is not null)await tx.CommitAsync(ct);
    }
}

