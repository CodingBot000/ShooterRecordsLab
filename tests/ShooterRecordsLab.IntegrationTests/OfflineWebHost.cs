using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShooterRecordsLab.Infrastructure.Persistence;

namespace ShooterRecordsLab.IntegrationTests;

internal static class OfflineWebHost
{
    public static IWebHostBuilder WithoutExternalDatabase(this IWebHostBuilder builder) => builder
        .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:ShooterRecords"] = null,
                ["Authentication:Jwt:Issuer"] = TestTokens.Issuer,
                ["Authentication:Jwt:Audience"] = TestTokens.Audience,
                ["Authentication:Jwt:SigningKeyBase64"] = TestTokens.KeyBase64,
                ["Authentication:Jwt:ValidationOnly"] = "false"
            }))
        .ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ShooterRecordsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ShooterRecordsDbContext>>();
            services.AddDbContext<ShooterRecordsDbContext>(options => options.UseSqlServer());
        });
}
