using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LiberationFleet.Server.Infrastructure.Data;

/// <summary>
/// Lets EF Core tools create <see cref="ApplicationDbContext"/> without starting the full web host.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        // Support running from repo root or the Server project directory.
        if (!File.Exists(Path.Combine(basePath, "appsettings.json"))
            && File.Exists(Path.Combine(basePath, "LiberationFleet.Server", "appsettings.json")))
        {
            basePath = Path.Combine(basePath, "LiberationFleet.Server");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=LiberationFleetDb;Trusted_Connection=true;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
