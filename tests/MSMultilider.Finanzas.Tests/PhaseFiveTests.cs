using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Multilider.Operations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.DTOs;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Models;
using MSMultilider.Finanzas.Services;
using Xunit;
namespace MSMultilider.Finanzas.Tests;
public sealed class PhaseFiveTests {
    private static VentaFinanciera Sale(int id=1)=>new(id,10,20,78,new(2026,11,4),"Preparada","CREDITO","BOB",1000,100,900,50,3,0,3,new(2026,12,31),"laravel-v1");
    private static OperationAuthorization Grant(Guid id,string kind="venta",Guid? parent=null)=>new(id,kind,78,"hash","tests","1",kind=="anulacion"?"Anulando":"Admitida",parent,Dataset:"fixture");
    private static string Snapshot=>JsonSerializer.Serialize(new{cliente=new{ci="F5-CI",nombre_completo="Cliente F5"},asesor=new{nombre_completo="Asesor F5",correo="adviser@example.test"},propiedad=new{codigo="F5-78"}},OperationProtocol.Json);
    private static async Task<OperationAuthorization> Prepare(AppDbContext db) {
        var g=Grant(Guid.NewGuid());var service=new FinancialOperations(db);
        await service.Prepare(g,new(Guid.NewGuid(),Sale()),default,Snapshot);await service.Activate(g.OperationId,78,default);return g;
    }
    private sealed class Provider:ILibelulaProvider {
        public int Registrations;public bool Paid;public bool LoseResponse;public Func<Task>? BeforeReturn;
        public async Task<JsonElement> Register(object data,CancellationToken ct) {
            Registrations++;if(BeforeReturn is not null)await BeforeReturn();if(LoseResponse)throw new HttpRequestException("Fixture lost acknowledgement");
            return JsonSerializer.SerializeToElement(new{url_pasarela_pagos="https://fixture.example.test/pay",id_transaccion="provider-fixture"});
        }
        public Task<bool> Verify(string reference,string currency,decimal amount,DateTime created,CancellationToken ct) {
            Assert.Equal("BOB",currency);Assert.Equal(.20m,amount);return Task.FromResult(Paid);
        }
    }
    private static TestPaymentRequest Request(Guid id,int payment)=>new(id,10,[payment],[],"F5-CI","70000000","Cliente","F5","fixture@example.test");
    [Fact] public async Task PreparationIsDurableIdempotentAndCannotResurrectAfterAbort() {
        using var factory=new ApiFactory();using var client=factory.Client();using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service=new FinancialOperations(db);var grant=Grant(Guid.NewGuid());var payload=new PrepareFinance(Guid.NewGuid(),Sale());
        await service.Prepare(grant,payload,default,Snapshot);await service.Prepare(grant,payload,default,Snapshot);
        Assert.Equal(1,await db.CuentasVentas.CountAsync());Assert.Equal(1,await db.PlanesPago.CountAsync());Assert.Equal(3,await db.Cuotas.CountAsync());
        Assert.Equal(1,await db.Pagos.CountAsync());Assert.Equal(1,await db.Egresos.CountAsync());Assert.Equal(0,await db.Ingresos.CountAsync());
        Assert.Equal("Preparada",(await db.CuentasVentas.SingleAsync()).Estado);
        await service.Abort(grant,default);
        await Assert.ThrowsAsync<BusinessException>(()=>service.Prepare(grant,payload,default,Snapshot));
        await Assert.ThrowsAsync<BusinessException>(()=>service.Activate(grant.OperationId,78,default));
        Assert.Equal("Abortada",(await db.Set<FinancialOperation>().SingleAsync()).State);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task TestPaymentNeverSettlesRealDebtAndUncertainRegistrationDoesNotRepeat(bool lost) {
        var provider=new Provider{LoseResponse=lost};using var factory=new ApiFactory{PaymentProvider=provider};using var client=factory.Client();
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var grant=await Prepare(db);
        var service=scope.ServiceProvider.GetRequiredService<PaymentTests>();var initial=await db.Pagos.SingleAsync();var request=Request(Guid.NewGuid(),initial.Id);
        var order=await service.Create(request,default);await service.Create(request,default);Assert.Equal(1,provider.Registrations);
        Assert.Equal(lost?"RegistroIncierto":"Registrada",order.State);
        await Assert.ThrowsAsync<BusinessException>(()=>service.Create(request with{OperacionId=Guid.NewGuid()},default));
        var closing=Grant(Guid.NewGuid(),"anulacion",grant.OperationId);
        await Assert.ThrowsAsync<BusinessException>(()=>new FinancialOperations(db).Close(closing,default));
        Assert.Equal(0,await service.Reconcile(order.Reference,default));Assert.Equal("PENDIENTE_PAGO",initial.Estado);
        provider.Paid=true;Assert.Equal(1,await service.Reconcile(order.Reference,default));
        Assert.Equal("PagadaPrueba",order.State);Assert.Equal(0,await db.Ingresos.CountAsync());
        Assert.Equal(100m,(await db.Pagos.SingleAsync()).Monto);Assert.Equal(900m,await db.Cuotas.SumAsync(x=>x.MontoCapital));
        Assert.All(await db.Cuotas.ToListAsync(),q=>Assert.Equal("Pendiente",q.Estado));
        await Assert.ThrowsAsync<BusinessException>(()=>new FinancialOperations(db).Close(closing,default));
        await new FinancialOperations(db).Close(closing with{OperationId=Guid.NewGuid()},default);Assert.Equal("Anulada",(await db.CuentasVentas.SingleAsync()).Estado);
    }
    [Fact] public async Task EarlyVerifiedCallbackCannotBeOverwrittenByRegistrationResponse() {
        var provider=new Provider{Paid=true};using var factory=new ApiFactory{PaymentProvider=provider};using var client=factory.Client();
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();await Prepare(db);
        provider.BeforeReturn=async()=>{using var other=factory.Services.CreateScope();await other.ServiceProvider.GetRequiredService<PaymentTests>().Reconcile(null,default);};
        var order=await scope.ServiceProvider.GetRequiredService<PaymentTests>().Create(Request(Guid.NewGuid(),(await db.Pagos.SingleAsync()).Id),default);
        Assert.Equal("PagadaPrueba",order.State);Assert.NotNull(order.PaymentUrl);Assert.Equal(0,await db.Ingresos.CountAsync());
    }
    [Fact] public async Task PartialInstallmentReplayAndBatchKeepOneIncomePerReceipt() {
        using var factory=new ApiFactory();using var client=factory.Client(ApiFactory.AllPermissions);
        using(var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();await Prepare(db);
            db.MetodosPago.Add(new(){Nombre="Efectivo"});db.CuentasBancarias.Add(new(){Nombre="Caja",Moneda="BOB"});await db.SaveChangesAsync();
        }
        var command=Guid.NewGuid();var body=new{operacion_id=Guid.NewGuid(),command_id=command,solicitud_id=command,nota_venta_id=1,cuota_id=1,monto=100,metodo_pago_id=1,cuenta_id=1,fecha_pago="2026-11-04"};
        await(await client.PostAsJsonAsync("/api/pagos",body)).Json();await(await client.PostAsJsonAsync("/api/pagos",body)).Json();
        using var verification=factory.Services.CreateScope();var check=verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1,await check.Ingresos.CountAsync());Assert.Equal(100m,await check.Ingresos.SumAsync(x=>x.Monto));
        Assert.Equal("Pendiente",(await check.Cuotas.FindAsync(1))!.Estado);
    }
    [Fact] public void LaravelCalendarKeepsLeapYearAndMonthOverflow() {
        var rows=Amortizacion.GenerarLaravel(900.01m,3,0,new(2028,1,31));
        Assert.Equal(new DateOnly(2028,3,2),rows[1].FechaVencimiento);Assert.Equal(new DateOnly(2028,4,2),rows[2].FechaVencimiento);
        Assert.Equal(900.01m,rows.Sum(x=>x.MontoCapital));Assert.Equal(300.01m,rows[^1].MontoCapital);Assert.Equal(0,rows[^1].SaldoCapital);
    }
    [Fact] public async Task PartialInstallmentsPayInterestFirstAndAmortizationPreservesPaidHistory() {
        using var factory=new ApiFactory();using var client=factory.Client(ApiFactory.AllPermissions);
        using(var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var grant=Grant(Guid.NewGuid());var operations=new FinancialOperations(db);
            await operations.Prepare(grant,new(Guid.NewGuid(),Sale() with{NumeroCuotas=1,TasaInteres=12}),default,Snapshot);
            await operations.Activate(grant.OperationId,78,default);
            db.MetodosPago.Add(new(){Nombre="Efectivo"});db.CuentasBancarias.Add(new(){Nombre="Caja",Moneda="BOB"});await db.SaveChangesAsync();
        }
        object Payment(decimal amount)=>new{solicitud_id=Guid.NewGuid(),nota_venta_id=1,cuota_id=1,monto=amount,metodo_pago_id=1,cuenta_id=1,fecha_pago="2026-11-04"};
        var first=await(await client.PostAsJsonAsync("/api/pagos",Payment(5))).Json();
        var plan=await(await client.GetAsync("/api/compat/ventas/1/plan-pago")).Json();
        Assert.Equal(900m,plan.GetProperty("resumen").GetProperty("saldo_capital").GetDecimal());
        Assert.Equal(4m,plan.GetProperty("plan_pago").GetProperty("cuotas")[0].GetProperty("interes_pendiente").GetDecimal());
        await(await client.PostAsJsonAsync("/api/pagos",Payment(25))).Json();
        plan=await(await client.GetAsync("/api/compat/ventas/1/plan-pago")).Json();
        Assert.Equal(879m,plan.GetProperty("resumen").GetProperty("saldo_capital").GetDecimal());
        Assert.Equal(21m,plan.GetProperty("plan_pago").GetProperty("cuotas")[0].GetProperty("capital_pagado").GetDecimal());
        var amort=await(await client.PostAsJsonAsync("/api/planes-pago/1/amortizar",new{solicitud_id=Guid.NewGuid(),monto=79m,metodo_pago_id=1,cuenta_id=1,fecha_pago="2026-11-05"})).Json();
        Assert.Equal(879m,amort.GetProperty("saldo_anterior").GetDecimal());Assert.Equal(800m,amort.GetProperty("saldo_nuevo").GetDecimal());
        var reversed=await client.PutAsJsonAsync($"/api/pagos/{first.GetProperty("id").GetInt32()}/cancelar",new{});
        Assert.Equal(System.Net.HttpStatusCode.Conflict,reversed.StatusCode);
        using var after=factory.Services.CreateScope();var check=after.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(900m,(await check.Cuotas.SingleAsync(q=>q.Estado=="Reprogramada")).MontoCapital);
        Assert.Equal(800m,(await check.Cuotas.SingleAsync(q=>q.Estado=="Pendiente")).MontoCapital);
        Assert.Equal(3,await check.Ingresos.CountAsync());
    }
}
