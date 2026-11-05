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
        DECLARE @version uniqueidentifier=NEWID(), @now datetime2=SYSUTCDATETIME();
        INSERT dbo.CuentasVentas (NotaVentaId,ClienteId,AsesorId,TipoVenta,Moneda,MontoLiquido,CuotaInicial,MontoComision,Estado,CreatedAt,UpdatedAt,Version)
        VALUES (1999999998,99,99,'CREDITO','BOB',900.01,0,0,'Activa',@now,@now,@version);
        DECLARE @account int=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.PlanesPago (CuentaVentaId,Monto,NumeroCuotas,TasaInteres,TasaMora,FechaInicio,FechaFinal,CreatedAt,UpdatedAt,Version)
        VALUES (@account,900.01,2,12,3,'20241031','20241130',@now,@now,@version);
        DECLARE @plan int=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.Cuotas (PlanPagoId,NumeroCuota,FechaVencimiento,MontoCuota,MontoCapital,MontoInteres,SaldoCapital,Estado,CreatedAt,UpdatedAt,Version)
        VALUES (@plan,1,'20241031',461.12,452.12,9.00,447.89,'Pagada',@now,@now,@version),
               (@plan,2,'20241130',452.37,447.89,4.48,0.00,'Pendiente',@now,@now,@version);
        """);
    await db.Database.MigrateAsync();
    var old=await db.CuentasVentas.SingleAsync(x=>x.NotaVentaId==1999999998);
    var plan=await db.PlanesPago.SingleAsync(x=>x.CuentaVentaId==old.Id);
    var rows=await db.Cuotas.Where(x=>x.PlanPagoId==plan.Id).OrderBy(x=>x.NumeroCuota).ToListAsync();
    if(old.MontoLiquido!=900.01m||plan.FechaFinal!=new DateOnly(2024,11,30)||rows.Count!=2||
        rows[0].MontoCapital!=452.12m||rows[0].MontoInteres!=9m||rows[0].SaldoCapital!=447.89m||rows[0].Estado!="Pagada"||
        rows[1].MontoCapital!=447.89m||rows[1].MontoInteres!=4.48m||rows[1].SaldoCapital!=0m||rows[1].Estado!="Pendiente")
        throw new InvalidOperationException("F5 upgrade changed the historical plan.");
    Console.WriteLine("F5-F13/O05: Finanzas preserved both historical installments, dates, amounts, balances and states.");
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
