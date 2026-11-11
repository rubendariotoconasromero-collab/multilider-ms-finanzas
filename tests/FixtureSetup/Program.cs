using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Models;
if(Environment.GetEnvironmentVariable("MULTILIDER_TEST_ISOLATED")!="1")throw new InvalidOperationException("Isolated runner required.");
var connection=Environment.GetEnvironmentVariable("MULTILIDER_FINANZAS_TEST_CONNECTION");
var config=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);const string prefix="multilider_finanzas_f5_test_";
if(config.DataSource!="localhost"||!config.InitialCatalog.StartsWith(prefix,StringComparison.Ordinal)||!Guid.TryParseExact(config.InitialCatalog[prefix.Length..],"N",out _))throw new InvalidOperationException("Unsafe test database.");
await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
if(args.Contains("drop")){await db.Database.EnsureDeletedAsync();Console.WriteLine("Temporary Finanzas database removed.");return;}
if(args.Contains("upgrade-f4")) {
    await db.GetService<IMigrator>().MigrateAsync("20261104160311_InitialCreate");
    await db.Database.ExecuteSqlRawAsync("""
        DECLARE @version uniqueidentifier='dc0e4e7b-33f2-4098-a8ad-74c483a5c0a1', @now datetime2=SYSUTCDATETIME();
        INSERT dbo.CuentasVentas (NotaVentaId,ClienteId,AsesorId,TipoVenta,Moneda,MontoLiquido,CuotaInicial,MontoComision,Estado,CreatedAt,UpdatedAt,Version)
        VALUES (1999999998,99,99,'CREDITO','BOB',900.01,0,0,'Activa',@now,@now,@version);
        DECLARE @account int=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.PlanesPago (CuentaVentaId,Monto,NumeroCuotas,TasaInteres,TasaMora,FechaInicio,FechaFinal,CreatedAt,UpdatedAt,Version)
        VALUES (@account,900.01,2,12,3,'20241031','20241130',@now,@now,@version);
        DECLARE @plan int=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.Reprogramaciones (PlanPagoId,UsuarioId,SolicitudId,Motivo,FechaReprogramacion,CuotaDesde,SaldoCapitalMomento,
            NuevaTasaInteres,NuevoNumeroCuotas,NuevaFechaInicio,CreatedAt,UpdatedAt,Version)
        VALUES (@plan,99,'da8831f8-d8cc-4e19-9b29-0f0599d79331',N'Histórico ficticio','20241125',2,447.89,11.75,1,'20241231',@now,@now,@version);
        DECLARE @revision int=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.Cuotas (PlanPagoId,NumeroCuota,ReprogramacionId,FechaVencimiento,MontoCuota,MontoCapital,MontoInteres,SaldoCapital,Estado,CreatedAt,UpdatedAt,Version)
        VALUES (@plan,1,NULL,'20241031',461.12,452.12,9.00,447.89,'Pagada',@now,@now,@version),
               (@plan,2,NULL,'20241130',452.37,447.89,4.48,0.00,'Reemplazada',@now,@now,@version),
               (@plan,2,@revision,'20241231',452.28,447.89,4.39,0.00,'Pendiente',@now,@now,@version);
        """);
    await db.Database.MigrateAsync();
    var old=await db.CuentasVentas.SingleAsync(x=>x.NotaVentaId==1999999998);
    var plan=await db.PlanesPago.SingleAsync(x=>x.CuentaVentaId==old.Id);
    var revision=await db.Reprogramaciones.SingleAsync(x=>x.PlanPagoId==plan.Id);
    var rows=await db.Cuotas.Where(x=>x.PlanPagoId==plan.Id).OrderBy(x=>x.Id).ToListAsync();
    var actual=rows.Select(x=>(x.NumeroCuota,x.ReprogramacionId,x.FechaVencimiento,x.MontoCuota,x.MontoCapital,x.MontoInteres,x.SaldoCapital,x.Estado));
    var expected=new (int,int?,DateOnly,decimal,decimal,decimal,decimal,string)[]{
        (1,null,new(2024,10,31),461.12m,452.12m,9m,447.89m,"Pagada"),
        (2,null,new(2024,11,30),452.37m,447.89m,4.48m,0m,"Reemplazada"),
        (2,revision.Id,new(2024,12,31),452.28m,447.89m,4.39m,0m,"Pendiente"),
    };
    if(old.MontoLiquido!=900.01m||plan.FechaFinal!=new DateOnly(2024,11,30)||
        revision.SolicitudId!=Guid.Parse("da8831f8-d8cc-4e19-9b29-0f0599d79331")||revision.SaldoCapitalMomento!=447.89m||
        revision.NuevaTasaInteres!=11.75m||!actual.SequenceEqual(expected)||
        rows.Any(x=>x.Version!=Guid.Parse("dc0e4e7b-33f2-4098-a8ad-74c483a5c0a1")||x.InteresAcreditado!=0m))
        throw new InvalidOperationException("F5 upgrade changed the historical plan.");
    Console.WriteLine("F5-F13/O05: Finanzas preserved the historical revision, three original and replacement rows, amounts, rounding, balances and states.");
} else {
    await db.Database.MigrateAsync();
}
if(db.Database.HasPendingModelChanges())throw new InvalidOperationException("Unmigrated financial model.");
if(args.Contains("verify")) {
    var duplicate=await db.Ingresos.GroupBy(x=>x.PagoId).AnyAsync(g=>g.Key!=null&&g.Count()>1);
    if(duplicate)throw new InvalidOperationException("Duplicate financial receipt.");
    Console.WriteLine("Financial SQL invariants verified.");return;
}
db.MetodosPago.Add(new MetodoPago{Nombre="Efectivo"});
db.CuentasBancarias.Add(new CuentaBancaria{Nombre="Caja BOB",Banco="Fixture",NumeroCuenta="BOB",Titular="Fixture",Moneda="BOB"});
db.CuentasBancarias.Add(new CuentaBancaria{Nombre="Caja USD",Banco="Fixture",NumeroCuenta="USD",Titular="Fixture",Moneda="USD"});
await db.SaveChangesAsync();Console.WriteLine("Unique Finanzas SQL fixture prepared.");
