using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShooterRecordsLab.Domain.Entities;

namespace ShooterRecordsLab.Infrastructure.Persistence.Configurations;

internal sealed class MatchResultConfiguration : IEntityTypeConfiguration<MatchResult>
{
    public void Configure(EntityTypeBuilder<MatchResult> builder)
    {
        builder.ToTable("MatchResults", "dbo", table =>
        {
            table.HasCheckConstraint("CK_MatchResults_Kills", "[Kills] BETWEEN 0 AND 10000");
            table.HasCheckConstraint("CK_MatchResults_Deaths", "[Deaths] BETWEEN 0 AND 10000");
            table.HasCheckConstraint("CK_MatchResults_Outcome", "[Outcome] IN (0, 1, 2)");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Outcome).HasConversion<int>();
        builder.HasOne<Player>().WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.MatchId, r.PlayerId }).IsUnique();
        builder.HasIndex(r => new { r.PlayerId, r.MatchId }).IncludeProperties(r => new { r.Kills, r.Deaths, r.Outcome });
    }
}
