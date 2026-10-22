using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShooterRecordsLab.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ShooterRecordsDbContext>
{
    public ShooterRecordsDbContext CreateDbContext(string[] args)
    {
        // No connection string: generating migrations/scripts must never connect to a database.
        var options = new DbContextOptionsBuilder<ShooterRecordsDbContext>().UseSqlServer().Options;
        return new ShooterRecordsDbContext(options);
    }
}
