using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MSMultilider.Finanzas.Data;
public sealed class DesignTimeFactory:IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config=new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json",optional:true).AddJsonFile("appsettings.Development.json",optional:true)
            .AddUserSecrets<DesignTimeFactory>().AddEnvironmentVariables().Build();
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(config.GetConnectionString("SqlServer")).Options);
    }
}

