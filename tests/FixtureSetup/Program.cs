using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Models;
if(Environment.GetEnvironmentVariable("MULTILIDER_TEST_ISOLATED")!="1")throw new InvalidOperationException("Isolated runner required.");
var connection=Environment.GetEnvironmentVariable("MULTILIDER_FINANZAS_TEST_CONNECTION");
var config=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);const string prefix="multilider_finanzas_f5_test_";
if(config.DataSource!="localhost"||!config.InitialCatalog.StartsWith(prefix,StringComparison.Ordinal)||!Guid.TryParseExact(config.InitialCatalog[prefix.Length..],"N",out _))throw new InvalidOperationException("Unsafe test database.");
await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
if(args.Contains("drop")){await db.Database.EnsureDeletedAsync();Console.WriteLine("Temporary Finanzas database removed.");return;}
await db.Database.MigrateAsync();
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
