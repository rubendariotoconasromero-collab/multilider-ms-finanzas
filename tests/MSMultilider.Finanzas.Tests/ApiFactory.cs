using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Services;

namespace MSMultilider.Finanzas.Tests;
public sealed class ApiFactory:WebApplicationFactory<Program> {
    public bool Development { get; init; }
    private readonly SqliteConnection connection=new("Data Source=:memory:");
    public const string TestKey="only-for-automated-tests-not-a-real-secret-0123456789";
    private readonly string storage=Path.Combine(Path.GetTempPath(),"multilider-test-"+Guid.NewGuid().ToString("N"));
    public HttpStatusCode RemoteStatus {get;set;}=HttpStatusCode.OK;
    public int RemoteCalls {get;private set;}
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment(Development ? "Development" : "Testing");
        builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?> {
            ["Jwt:Key"]=TestKey,["Jwt:Issuer"]="tests",["Jwt:Audience"]="tests",["Storage:Path"]=storage,
            ["Logging:LogLevel:Default"]="Error",["Logging:LogLevel:Microsoft"]="Error",["Logging:LogLevel:Microsoft.AspNetCore"]="Error"
        }));
        builder.ConfigureServices(services=>{
            connection.Open();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o=>o.UseSqlite(connection));
            services.AddHttpClient<ComercialClient>().ConfigurePrimaryHttpMessageHandler(()=>new Handler(this));
        });
    }
    public HttpClient Client(params string[] permissions) {
        var client=CreateClient();
        using var scope=Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        if(permissions.Length>0) client.DefaultRequestHeaders.Authorization=new("Bearer",Token(permissions));
        return client;
    }
    public static readonly string[] AllPermissions=["acceso_clientes","acceso_asesores","acceso_ventas","acceso_historial_ventas",
        "acceso_contratos","acceso_entregas","acceso_pagos","acceso_ingresos","acceso_egresos"];
    public static string Token(string[] permissions,string issuer="tests",bool expired=false) {
        var claims=permissions.Select(p=>new Claim("permission",p)).Append(new Claim("sub","1"));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer,"tests",claims,
            DateTime.UtcNow.AddMinutes(-5),DateTime.UtcNow.AddMinutes(expired?-1:10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),SecurityAlgorithms.HmacSha256)));
    }
    private sealed class Handler(ApiFactory factory):HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            factory.RemoteCalls++;
            if(factory.RemoteStatus!=HttpStatusCode.OK)return Task.FromResult(new HttpResponseMessage(factory.RemoteStatus));
            object body;
            var segments=request.RequestUri!.AbsolutePath.Split('/');
            var id=int.Parse(segments[3]);
            var credit=id!=2;
            body=new {id,cliente_id=10,asesor_id=20,propiedad_id=id,fecha="2026-11-01",estado="Activa",
                tipo_venta=credit?"CREDITO":"CONTADO",moneda="BOB",monto_liquido=1000m,cuota_inicial=credit?100m:0m,
                saldo_credito=credit?900m:0m,monto_comision=100m,numero_cuotas=credit?3:0,tasa_interes=0m,tasa_mora=0m,
                fecha_inicio_pago=credit?"2026-12-01":null};
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(body)});
        }
    }
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if(disposing) {
            connection.Dispose();
            var root=Path.GetFullPath(storage);
            if(!root.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(root).StartsWith("multilider-test-"))throw new InvalidOperationException("Unexpected test storage path.");
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
}
public static class HttpTest {
    public static async Task<JsonElement> Json(this HttpResponseMessage response) {
        var body=await response.Content.ReadAsStringAsync();
        Xunit.Assert.True(response.IsSuccessStatusCode,$"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}

