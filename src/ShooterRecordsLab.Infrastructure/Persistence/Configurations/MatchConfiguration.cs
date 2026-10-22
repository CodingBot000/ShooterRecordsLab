using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShooterRecordsLab.Domain.Entities;

namespace ShooterRecordsLab.Infrastructure.Persistence.Configurations;

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Matches_ExternalMatchId", "DATALENGTH([ExternalMatchId]) BETWEEN 2 AND 128 AND [ExternalMatchId] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^-A-Z0-9_]%'");
            table.HasCheckConstraint("CK_Matches_PlayedAtUtc", "DATEPART(TZOFFSET, [PlayedAtUtc]) = 0");
            table.HasCheckConstraint("CK_Matches_CreatedAtUtc", "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
            table.HasCheckConstraint("CK_Matches_Fingerprint", "DATALENGTH([PayloadFingerprint]) = 64 AND [PayloadFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-F0-9]%'");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.ExternalMatchId).HasMaxLength(64).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(m => m.PayloadFingerprint).HasColumnType("char(64)").IsRequired();
        builder.Property(m => m.PlayedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(m => m.CreatedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.HasIndex(m => m.ExternalMatchId).IsUnique();
        builder.HasIndex(m => new { m.PlayedAtUtc, m.Id }).IsDescending(true, true);
        builder.HasMany(m => m.Results).WithOne().HasForeignKey(r => r.MatchId).OnDelete(DeleteBehavior.Cascade);
    }
}
