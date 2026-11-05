using Multilider.Operations;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Infrastructure;
using MSMultilider.Finanzas.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o=>Multilider.Identity.SessionValidation.Configure(o,builder.Configuration));
builder.Services.AddAuthorization(o => {
    foreach (var permission in new[] { "acceso_clientes","acceso_asesores","acceso_ventas","acceso_historial_ventas",
        "acceso_contratos","acceso_entregas","acceso_pagos","acceso_ingresos","acceso_egresos" })
        o.AddPolicy(permission,p=>p.RequireAuthenticatedUser().RequireClaim("permission",permission));
});
builder.Services.AddCors(o=>o.AddDefaultPolicy(p=>p.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => {
    o.SwaggerDoc("v1",new OpenApiInfo { Title="MSMultilider.Finanzas",Version="v1" });
    o.AddSecurityDefinition("Bearer",new OpenApiSecurityScheme { Type=SecuritySchemeType.Http, Scheme="bearer",BearerFormat="JWT" });
    o.AddSecurityRequirement(document=>new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer",document)] = [] });
});
builder.Services.AddScoped<FinanzasService>();
builder.Services.AddSingleton<ILegacyFinancialGate,LegacyFinancialGate>();
builder.Services.AddScoped<FinancialOperations>();
builder.Services.AddScoped<FinanceReads>();
builder.Services.AddScoped<PaymentTests>();
builder.Services.AddHostedService<PaymentTestReconciler>();
builder.Services.AddRateLimiter(o=> {
    o.RejectionStatusCode=429;
    o.AddPolicy("public-payments",context=>System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new System.Threading.RateLimiting.FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
builder.Services.AddScoped<ILibelulaProvider,LibelulaProvider>();
builder.Services.AddHttpClient("libelula",c=>c.Timeout=TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(()=>new SocketsHttpHandler{AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(2)});
builder.Services.AddHttpClient<ComercialClient>((sp,c)=> {
    c.BaseAddress=new Uri(sp.GetRequiredService<IConfiguration>()["Services:Comercial"]!);
    c.Timeout=TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("identity-status",c=> {
    c.BaseAddress=new Uri((builder.Configuration["Services:Seguridad"] ?? "http://localhost:5211").TrimEnd('/')+"/");
    c.Timeout=TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(()=>new SocketsHttpHandler {AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(2)});
builder.Services.AddSingleton<OperationChannel>();
builder.WebHost.ConfigureKestrel((context,options)=>OperationProtocol.ConfigureTls(context,options,builder.Configuration));
var app=builder.Build();
if(args.Contains("--preflight-f5")) {
    await using var scope=app.Services.CreateAsyncScope();
    await OperationProtocol.PrintPreflight(scope.ServiceProvider.GetRequiredService<AppDbContext>(),builder.Configuration,"finanzas");
    return;
}
if(args.Contains("--migrate-finance")) {
    await using var scope=app.Services.CreateAsyncScope();
    var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await OperationProtocol.PrintPreflight(db,builder.Configuration,"finanzas");
    await db.Database.OpenConnectionAsync();
    if(!(await db.Database.GetAppliedMigrationsAsync()).Any()) {
        await using var command=db.Database.GetDbConnection().CreateCommand();
        command.CommandText="SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0 AND name<>'__EFMigrationsHistory'";
        if(Convert.ToInt32(await command.ExecuteScalarAsync())!=0)throw new InvalidOperationException("Unexpected tables without financial migration history.");
    }
    await db.Database.CloseConnectionAsync();await db.Database.MigrateAsync();return;
}

app.UseExceptionHandler();
if(app.Environment.IsDevelopment()) { app.UseSwagger();app.UseSwaggerUI(); }
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<OperationProtocolMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<FinancialMutation>();
app.MapGet("/health",()=>Results.Ok(new {service="Finanzas",status="ok"})).AllowAnonymous();
app.MapGet("/health/ready",async(AppDbContext db,CancellationToken ct)=> {
    if (!await db.Database.CanConnectAsync(ct)) return Results.StatusCode(503);
    return (await db.Database.GetPendingMigrationsAsync(ct)).Any()
        ? Results.StatusCode(503) : Results.Ok(new {status="ready"});
}).RequireAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }

