using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MSMultilider.Finanzas.Data;
using MSMultilider.Finanzas.Models;
using Xunit;
namespace MSMultilider.Finanzas.Tests;
public sealed class SqlServerFactAttribute:FactAttribute {
    public SqlServerFactAttribute() {
        if(Environment.GetEnvironmentVariable("MULTILIDER_SQLSERVER_TESTS")!="1")
            Skip="Prueba opcional: MULTILIDER_SQLSERVER_TESTS=1; usa una base temporal nueva en SQL Server local.";
    }
}
public sealed class SqlServerMigrationTests {
    [SqlServerFact]
    public async Task MigracionesIndicesYConcurrenciaEnSqlServerReal() {
        const string prefix="multilider_finanzas_test_";
        var name=prefix+Guid.NewGuid().ToString("N");
        var connection=new SqlConnectionStringBuilder("Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10");
        connection.InitialCatalog=name;
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db=new AppDbContext(options);
        try {
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var row=new CuentaVenta {NotaVentaId=1,ClienteId=1,AsesorId=1,TipoVenta="CONTADO",MontoLiquido=100};
            db.CuentasVentas.Add(row);await db.SaveChangesAsync();
            db.CuentasVentas.Add(new CuentaVenta {NotaVentaId=1,TipoVenta="CONTADO"});
            await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            await using var other=new AppDbContext(options);
            var first=await db.CuentasVentas.SingleAsync();var stale=await other.CuentasVentas.SingleAsync();
            first.Estado="Anulada";stale.Estado="Otra";
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>other.SaveChangesAsync());
        } finally {
            // This test only drops the unique, newly created test database.
            if(!name.StartsWith(prefix,StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..],"N",out _))
                throw new InvalidOperationException("Unexpected test database name.");
            await db.Database.EnsureDeletedAsync();
        }
    }
}

