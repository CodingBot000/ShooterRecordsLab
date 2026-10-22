using Microsoft.EntityFrameworkCore;
using ShooterRecordsLab.Domain.Entities;

namespace ShooterRecordsLab.Infrastructure.Persistence;

public sealed class ShooterRecordsDbContext(DbContextOptions<ShooterRecordsDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShooterRecordsDbContext).Assembly);
}
