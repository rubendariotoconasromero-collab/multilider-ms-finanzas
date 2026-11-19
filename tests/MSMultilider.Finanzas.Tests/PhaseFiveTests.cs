using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        public int VerificationTimeouts;
        public int Verifications;
        public decimal ExpectedAmount = .20m;
        public JsonElement Registration;
        public async Task<JsonElement> Register(object data,CancellationToken ct) {
            Registrations++;
            Registration = JsonSerializer.SerializeToElement(data);
            if(BeforeReturn is not null)await BeforeReturn();if(LoseResponse)throw new HttpRequestException("Fixture lost acknowledgement");
            return JsonSerializer.SerializeToElement(new{url_pasarela_pagos="https://fixture.example.test/pay",id_transaccion="provider-fixture"});
        }
        public Task<bool> Verify(string reference,string currency,decimal amount,DateTime created,CancellationToken ct) {
            Verifications++;
            Assert.Equal("BOB",currency);Assert.Equal(ExpectedAmount,amount);
            if (VerificationTimeouts > 0) {
                VerificationTimeouts--;
                throw new TaskCanceledException("Fixture provider read timed out.", new TimeoutException());
            }
            return Task.FromResult(Paid);
        }
    }
    private static TestPaymentRequest Request(Guid id,int payment)=>new(id,10,[payment],[],"F5-CI","70000000","Cliente","F5","fixture@example.test");

    [Fact]
    public async Task ProviderProofRequiresTheConfiguredMerchantAndExactCurrencyAndAmount()
    {
        const string reference = "TEMP-0a968553-3bc9-4be3-9ac0-68043115620a";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Libelula:BaseUrl"] = "https://payments.example.test",
            ["Libelula:ApiKey"] = "fixture-merchant-a"
        }).Build();
        var responses = new Queue<object>([
            Array.Empty<object>(),
            new[] { new { identificador = reference, pagado = true, moneda = "USD", valor_total = "0.20" } },
            new[] { new { identificador = reference, pagado = true, moneda = "BOB", valor_total = "0.204" } },
            new[] { new { identificador = reference, pagado = true, moneda = "BOB", valor_total = "0.20" } },
        ]);
        using var http = new HttpClient(new MerchantFixture(async request => {
            Assert.Equal("https://payments.example.test/rest/deuda/consultar_deudas/por_fechas", request.RequestUri!.AbsoluteUri);
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("fixture-merchant-a", body.GetProperty("appkey").GetString());
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = JsonContent.Create(new { error = 0, datos = responses.Dequeue() })
            };
        }));
        var provider = new LibelulaProvider(config, new MerchantFactory(http));
        var date = new DateTime(2026, 11, 4, 15, 0, 0, DateTimeKind.Utc);
        for (var attempt = 0; attempt < 3; attempt++)
            Assert.False(await provider.Verify(reference, "BOB", .20m, date, default));
        Assert.True(await provider.Verify(reference, "BOB", .20m, date, default));
        Assert.Empty(responses);
    }

    private sealed class MerchantFixture(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }

    private sealed class MerchantFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
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

    [Theory]
    [InlineData("GET", "", null)]
    [InlineData("GET", "?identificador=%20%20&transaction_id=", null)]
    [InlineData("GET", "?identificador=x&identificador=y", null)]
    [InlineData("POST", "", "{}")]
    [InlineData("POST", "", "{\"identificador\":null,\"transaction_id\":\" \"}")]
    [InlineData("POST", "", "{\"transaction_id\":[\"no-es-texto\"]}")]
    [InlineData("POST", "", "{\"identificador\":")]
    public async Task CallbackRequiresAValidIdentifierBeforeRecordingAnEvent(string method, string query, string? body) {
        var provider = new Provider();
        using var factory = new ApiFactory { PaymentProvider = provider };
        using var client = factory.Client();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/public/pagos/callback" + query);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Set<PaymentTestInbox>().ToListAsync());
        Assert.Empty(await db.Set<PaymentTestOrder>().ToListAsync());
        Assert.Equal(0, provider.Registrations);
        Assert.Equal(0, provider.Verifications);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST form")]
    [InlineData("POST JSON")]
    [InlineData("POST con transaction_id en consulta")]
    public async Task ValidCallbackConfirmsOnlyFortyCentsForTwoConcepts(string method) {
        var provider = new Provider { ExpectedAmount = .40m };
        using var factory = new ApiFactory { PaymentProvider = provider };
        using var client = factory.Client();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Prepare(db);
        var payment = await db.Pagos.AsNoTracking().SingleAsync();
        async Task<decimal[]> QuotaBalances() => await db.Cuotas.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => x.SaldoCapital).ToArrayAsync();
        var balances = await QuotaBalances();
        var quota = await db.Cuotas.AsNoTracking().OrderBy(x => x.Id).FirstAsync();
        var operation = Guid.NewGuid();
        var input = new { operacion_id = operation, cliente_id = 10, pago_ids = new[] { payment.Id }, cuota_ids = new[] { quota.Id },
            ci_pagador = "F5-CI", telefono_pagador = "70000000", nombres_pagador = "Cliente", apellidos_pagador = "F5", correo_pagador = "fixture@example.test" };
        var order = await (await client.PostAsJsonAsync("/api/public/pagos/procesar", input)).Json();
        Assert.True(order.GetProperty("es_prueba").GetBoolean());
        Assert.Equal(.40m, order.GetProperty("monto_prueba").GetDecimal());
        var lines = provider.Registration.GetProperty("lineas_detalle_deuda").EnumerateArray().ToArray();
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => {
            Assert.Equal(.20m, line.GetProperty("costo_unitario").GetDecimal());
            Assert.Equal(1, line.GetProperty("cantidad").GetInt32());
        });

        var reference = order.GetProperty("id_transaccion").GetString()!;
        await (await client.PostAsJsonAsync("/api/public/pagos/procesar", input)).Json();
        var path = "/api/public/pagos/callback";
        var query = "?identificador=" + Uri.EscapeDataString(reference);
        Task<HttpResponseMessage> Notify() => method switch {
            "GET" => client.GetAsync(path + query),
            "POST form" => client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> { ["identificador"] = reference })),
            "POST JSON" => client.PostAsJsonAsync(path, new { identificador = reference }),
            _ => client.PostAsJsonAsync(path + "?transaction_id=provider-fixture", new { }),
        };
        using (var pending = await Notify()) {
            Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
            Assert.True(pending.Headers.CacheControl?.NoStore);
        }
        Assert.Equal("Registrada", (await db.Set<PaymentTestOrder>().AsNoTracking().SingleAsync()).State);
        provider.Paid = true;
        using (var response = await Notify()) Assert.True((await response.Json()).GetProperty("ok").GetBoolean());
        var confirmed = await (await client.PostAsJsonAsync("/api/public/pagos/confirmar-retorno", new { id_transaccion = reference })).Json();
        Assert.Equal("PAGADO_PRUEBA", confirmed.GetProperty("estado").GetString());
        Assert.Equal(.40m, confirmed.GetProperty("monto_prueba").GetDecimal());
        Assert.False(confirmed.GetProperty("deuda_real_liquidada").GetBoolean());
        var notice = await db.Set<PaymentTestInbox>().AsNoTracking().SingleAsync();
        if (method == "POST con transaction_id en consulta") Assert.Equal("provider-fixture", notice.ProviderId);
        else Assert.Equal("Conciliada", notice.State);
        Assert.Equal(1, provider.Registrations);
        Assert.Equal(2, provider.Verifications);
        var actualPayment = await db.Pagos.AsNoTracking().SingleAsync();
        Assert.Equal("PENDIENTE_PAGO", actualPayment.Estado);
        Assert.Equal(payment.Monto, actualPayment.Monto);
        Assert.Equal(balances, await QuotaBalances());
        Assert.All(await db.Cuotas.AsNoTracking().ToListAsync(), row => Assert.Equal("Pendiente", row.Estado));
        Assert.Empty(await db.Ingresos.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CallbackPersistenceFailureRequiresRetryAndTimeoutKeepsPaymentPending()
    {
        var provider = new Provider { Paid = true, VerificationTimeouts = 1 };
        using var factory = new ApiFactory { PaymentProvider = provider };
        using var client = factory.Client();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Prepare(db);
        var initial = await db.Pagos.SingleAsync();
        var service = scope.ServiceProvider.GetRequiredService<PaymentTests>();
        var order = await service.Create(Request(Guid.NewGuid(), initial.Id), default);
        var callback = new { identificador = order.Reference, transaction_id = "provider-fixture", error = "0" };
        var lateCallback = new { identificador = order.Reference, transaction_id = "provider-fixture", error = "1", cancel_order = "1" };

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fixture_reject_callback BEFORE INSERT ON PaymentTestInbox
            BEGIN SELECT RAISE(ABORT, 'fixture inbox unavailable'); END
            """);
        try {
            var failed = await client.PostAsJsonAsync("/api/public/pagos/callback", callback);
            Assert.False(failed.IsSuccessStatusCode);
            Assert.Empty(await db.Set<PaymentTestInbox>().ToListAsync());
            Assert.Equal(1, provider.VerificationTimeouts);
        } finally {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fixture_reject_callback");
        }

        await (await client.PostAsJsonAsync("/api/public/pagos/callback", callback)).Json();
        var pending = await db.Set<PaymentTestOrder>().AsNoTracking().SingleAsync();
        Assert.Equal("Registrada", pending.State);
        Assert.Equal("Pendiente", (await db.Set<PaymentTestInbox>().AsNoTracking().SingleAsync()).State);
        await (await client.PostAsJsonAsync("/api/public/pagos/callback", callback)).Json();
        await (await client.PostAsJsonAsync("/api/public/pagos/callback", lateCallback)).Json();
        Assert.Equal("PagadaPrueba", (await db.Set<PaymentTestOrder>().AsNoTracking().SingleAsync()).State);
        Assert.Equal(2, await db.Set<PaymentTestInbox>().CountAsync());
        Assert.Equal("PENDIENTE_PAGO", (await db.Pagos.AsNoTracking().SingleAsync()).Estado);
        Assert.Equal(100m, (await db.Pagos.AsNoTracking().SingleAsync()).Monto);
        Assert.Empty(await db.Ingresos.ToListAsync());
        Assert.Equal(1, provider.Registrations);
    }

    [Fact]
    public async Task ManualLedgerEditReplaysOnceAndAutomaticAmountsCannotBeChanged()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client(ApiFactory.AllPermissions);
        int automaticIncome;
        int automaticExpense;
        using (var setup = factory.Services.CreateScope()) {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            await Prepare(db);
            automaticExpense = (await db.Egresos.SingleAsync()).Id;
            var income = new Ingreso { Origen = "AUTOMATICO", Monto = 100, Moneda = "BOB",
                Fecha = new(2026, 11, 4), Concepto = "Cuota fixture", Categoria = "CUOTA" };
            db.Ingresos.Add(income);
            await db.SaveChangesAsync();
            automaticIncome = income.Id;
        }

        foreach (var (route, automatic) in new[] { ("ingresos", automaticIncome), ("egresos", automaticExpense) }) {
            var original = new { fecha = "2026-11-04", concepto = "Movimiento manual fixture", categoria = "OTRO", monto = 25m, moneda = "BOB" };
            var created = await (await client.PostAsJsonAsync("/api/" + route, original)).Json();
            var id = created.GetProperty("id").GetInt32();
            var uri = $"/api/{route}/{id}";
            var edit = new { original.fecha, original.concepto, original.categoria, monto = 26.50m, original.moneda, solicitud_id = Guid.NewGuid() };
            await (await client.PutAsJsonAsync(uri, edit)).Json();
            using var replay = await client.PutAsJsonAsync(uri, edit);
            Assert.True(replay.IsSuccessStatusCode);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
            var changed = await client.PutAsJsonAsync(uri, edit with { monto = 999m });
            Assert.Equal(System.Net.HttpStatusCode.Conflict, changed.StatusCode);
            var saved = await (await client.GetAsync(uri)).Json();
            Assert.Equal(26.50m, saved.GetProperty("monto").GetDecimal());
            var protectedAmount = await client.PutAsJsonAsync($"/api/{route}/{automatic}", edit with { solicitud_id = Guid.NewGuid() });
            Assert.Equal(System.Net.HttpStatusCode.Conflict, protectedAmount.StatusCode);
        }
        using var verification = factory.Services.CreateScope();
        var final = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await final.Ingresos.CountAsync());
        Assert.Equal(2, await final.Egresos.CountAsync());
        Assert.Equal(100m, (await final.Ingresos.FindAsync(automaticIncome))!.Monto);
        Assert.Equal(50m, (await final.Egresos.FindAsync(automaticExpense))!.Monto);
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
