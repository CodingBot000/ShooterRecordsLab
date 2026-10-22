using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShooterRecordsLab.Domain.Entities;

namespace ShooterRecordsLab.Infrastructure.Persistence.Configurations;

internal sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Players_DisplayName", "LEN(LTRIM(RTRIM([DisplayName]))) > 0");
            table.HasCheckConstraint("CK_Players_CreatedAtUtc", "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.DisplayName).HasMaxLength(64).IsRequired();
        builder.Property(p => p.CreatedAtUtc).HasColumnType("datetimeoffset(7)");
    }
}
