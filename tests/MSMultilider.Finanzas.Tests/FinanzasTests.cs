using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MSMultilider.Finanzas.Services;
using Xunit;
namespace MSMultilider.Finanzas.Tests;
public sealed class FinanzasTests {
    private static async Task Setup(HttpClient client,int venta=1) {
        await(await client.PostAsJsonAsync("/api/metodos-pago",new{nombre="Efectivo"})).Json();
        await(await client.PostAsJsonAsync("/api/cuentas-bancarias",new{nombre="Caja",banco="Local",numero_cuenta="001",titular="Empresa",moneda="BOB"})).Json();
        await(await client.PostAsJsonAsync($"/api/ventas/{venta}/registrar",new{})).Json();
    }
    private static object Pago(decimal monto,int venta=1,int? cuota=null,Guid? id=null,int cuenta=1) =>
        new{solicitud_id=id??Guid.NewGuid(),nota_venta_id=venta,monto,cuota_id=cuota,metodo_pago_id=1,cuenta_id=cuenta,fecha_pago="2026-11-04"};
    [Theory]
    [InlineData(1000,3,0)]
    [InlineData(100000,360,12)]
    [InlineData(0.03,3,0)]
    [InlineData(1234.56,17,8.5)]
    public void PlanConservaCapitalYFechas(decimal capital,int meses,decimal tasa) {
        var cuotas=Amortizacion.Generar(capital,meses,tasa,new DateOnly(2026,1,31));
        Assert.Equal(capital,cuotas.Sum(x=>x.MontoCapital));Assert.Equal(0,cuotas[^1].SaldoCapital);
        Assert.All(cuotas,x=>Assert.Equal(x.MontoCapital+x.MontoInteres,x.MontoCuota));
        if(meses>=3) {Assert.Equal(new DateOnly(2026,2,28),cuotas[1].FechaVencimiento);Assert.Equal(new DateOnly(2026,3,31),cuotas[2].FechaVencimiento);}
    }
    [Fact] public async Task AutorizacionValidaFirmaEmisorYPermisos() {
        using var app=new ApiFactory();using var client=app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/pagos")).StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",ApiFactory.Token(["otro"]));
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/pagos")).StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",ApiFactory.Token(["acceso_pagos"]));
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/ventas/1/anular",new{})).StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",ApiFactory.Token(["acceso_pagos"],issuer:"otro"));
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/pagos")).StatusCode);
    }
    [Fact] public async Task RegistrarVentaEsIdempotenteYGeneraComision() {
        using var app=new ApiFactory();using var client=app.Client(ApiFactory.AllPermissions);
        await Setup(client);
        await(await client.PostAsJsonAsync("/api/ventas/1/registrar",new{})).Json();
        var plan=await(await client.GetAsync("/api/ventas/1/plan-pago")).Json();
        Assert.Equal(3,plan.GetProperty("cuotas").GetArrayLength());
        var egresos=await(await client.GetAsync("/api/egresos")).Json();Assert.Equal(1,egresos.GetProperty("total").GetInt32());
        Assert.Equal(100,egresos.GetProperty("data")[0].GetProperty("monto").GetDecimal());
    }
    [Fact] public async Task PagoInicialCuotaYReintentoNoDuplicanIngresos() {
        using var app=new ApiFactory();using var client=app.Client(ApiFactory.AllPermissions);
        await Setup(client);
        var request=Pago(100,id:Guid.NewGuid());
        var one=await(await client.PostAsJsonAsync("/api/pagos",request)).Json();
        var two=await(await client.PostAsJsonAsync("/api/pagos",request)).Json();
        Assert.Equal(one.GetProperty("id").GetInt32(),two.GetProperty("id").GetInt32());
        await(await client.PostAsJsonAsync("/api/pagos",Pago(300,cuota:1))).Json();
        var balance=await(await client.GetAsync("/api/cuentas-bancarias/1/saldo")).Json();
        Assert.Equal(400,balance.GetProperty("saldo").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/pagos",Pago(300,cuota:1))).StatusCode);
        var ingresos=await(await client.GetAsync("/api/ingresos")).Json();Assert.Equal(2,ingresos.GetProperty("total").GetInt32());
    }
    [Fact] public async Task RechazaPagoEnOtraMonedaYSolicitudReutilizada() {
        using var app=new ApiFactory();using var client=app.Client(ApiFactory.AllPermissions);
        await Setup(client);
        await(await client.PostAsJsonAsync("/api/cuentas-bancarias",new{nombre="USD",banco="Local",numero_cuenta="002",titular="Empresa",moneda="USD"})).Json();
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await client.PostAsJsonAsync("/api/pagos",Pago(100,cuenta:2))).StatusCode);
        var id=Guid.NewGuid();await(await client.PostAsJsonAsync("/api/pagos",Pago(100,id:id))).Json();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/pagos",Pago(101,id:id))).StatusCode);
        var summary=await(await client.GetAsync("/api/ingresos/resumen")).Json();Assert.Equal(100,summary[0].GetProperty("total").GetDecimal());
    }
    [Fact] public async Task AmortizarYReprogramarUsaSaldoActualSinRevivirCapital() {
        using var app=new ApiFactory();using var client=app.Client(ApiFactory.AllPermissions);
        await Setup(client);await(await client.PostAsJsonAsync("/api/pagos",Pago(100))).Json();
        var key=Guid.NewGuid();
        var request=new{solicitud_id=key,monto=150m,metodo_pago_id=1,cuenta_id=1,fecha_pago="2026-11-04"};
        await(await client.PostAsJsonAsync("/api/planes-pago/1/amortizar",request)).Json();
        await(await client.PostAsJsonAsync("/api/planes-pago/1/amortizar",request)).Json();
        var change=new{solicitud_id=Guid.NewGuid(),motivo="Ajuste",fecha_reprogramacion="2026-11-04",
            cuota_desde=1,nueva_tasa_interes=0,nuevo_numero_cuotas=5,nueva_fecha_inicio="2026-12-01"};
        await(await client.PostAsJsonAsync("/api/planes-pago/1/reprogramar",change)).Json();
        await(await client.PostAsJsonAsync("/api/planes-pago/1/reprogramar",change)).Json();
        var summary=await(await client.GetAsync("/api/ventas/1/resumen")).Json();
        Assert.Equal(750,summary.GetProperty("saldo_capital").GetDecimal());
        Assert.Equal(250,summary.GetProperty("total_pagado").GetDecimal());
        var history=await(await client.GetAsync("/api/planes-pago/1/reprogramaciones")).Json();Assert.Equal(1,history.GetArrayLength());
        var plan=await(await client.GetAsync("/api/ventas/1/plan-pago")).Json();
        Assert.Equal(8,plan.GetProperty("cuotas").GetArrayLength()); // historical rows retained
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/pagos",Pago(300,cuota:1))).StatusCode);
    }
    [Fact] public async Task CancelarPagoRevierteIngresoYPuedePagarseDeNuevo() {
        using var app=new ApiFactory();using var client=app.Client(ApiFactory.AllPermissions);
        await Setup(client,2);await(await client.PostAsJsonAsync("/api/pagos",Pago(1000,venta:2))).Json();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/ventas/2/anular",new{})).StatusCode);
        await(await client.PutAsJsonAsync("/api/pagos/1/cancelar",new{})).Json();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/pagos/1/procesar",new{
            solicitud_id=Guid.NewGuid(),metodo_pago_id=1,cuenta_id=1,fecha_pago="2026-11-04"})).StatusCode);
        var saldo=await(await client.GetAsync("/api/cuentas-bancarias/1/saldo")).Json();Assert.Equal(0,saldo.GetProperty("saldo").GetDecimal());
        await(await client.PostAsJsonAsync("/api/pagos",Pago(1000,venta:2))).Json();
        var saldo2=await(await client.GetAsync("/api/cuentas-bancarias/1/saldo")).Json();Assert.Equal(1000,saldo2.GetProperty("saldo").GetDecimal());
    }
    [Fact] public async Task AnularAntesDeRegistrarImpideResucitarLaDeuda() {
        using var app=new ApiFactory();using var client=app.Client(ApiFactory.AllPermissions);
        await(await client.PostAsJsonAsync("/api/ventas/1/anular",new{})).Json();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/ventas/1/registrar",new{})).StatusCode);
    }
    [Fact] public async Task EgresoComisionSeDesembolsaUnaVezSinBorrarHistorial() {
        using var app=new ApiFactory();using var client=app.Client(ApiFactory.AllPermissions);
        await Setup(client);
        await(await client.PutAsJsonAsync("/api/egresos/1/pagar",new{cuenta_id=1})).Json();
        await(await client.PutAsJsonAsync("/api/egresos/1/pagar",new{cuenta_id=1})).Json();
        var saldo=await(await client.GetAsync("/api/cuentas-bancarias/1/saldo")).Json();Assert.Equal(-100,saldo.GetProperty("saldo").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync("/api/egresos/1/anular",new{})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/ventas/1/anular",new{})).StatusCode);
    }
}

