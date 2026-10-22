using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ShooterRecordsLab.Domain.Entities;
using ShooterRecordsLab.Infrastructure.Persistence;
using ShooterRecordsLab.Infrastructure.Repositories;

namespace ShooterRecordsLab.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void SqlServer_model_and_migration_script_can_be_generated_without_a_connection()
    {
        using var db = new DesignTimeDbContextFactory().CreateDbContext([]);
        Assert.Null(db.Database.GetConnectionString());
        var model = db.GetServiceModel();
        var match = model.FindEntityType(typeof(Match))!;
        Assert.Contains(match.GetIndexes(), index => index.IsUnique && index.Properties.SingleOrDefault()?.Name == nameof(Match.ExternalMatchId));
        Assert.Equal("nvarchar(64)", match.FindProperty(nameof(Match.ExternalMatchId))!.GetColumnType());
        Assert.Equal("datetimeoffset(7)", match.FindProperty(nameof(Match.PlayedAtUtc))!.GetColumnType());
        var result = model.FindEntityType(typeof(MatchResult))!;
        Assert.Equal(2, result.GetForeignKeys().Count());
        Assert.Contains(result.GetIndexes(), index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(new[] { "MatchId", "PlayerId" }));
        Assert.Equal(3, result.GetCheckConstraints().Count());
        var script = db.Database.GenerateCreateScript();
        Assert.Contains("CREATE UNIQUE INDEX [IX_Matches_ExternalMatchId]", script, StringComparison.Ordinal);
        Assert.Contains("CHECK ([Kills] BETWEEN 0 AND 10000)", script, StringComparison.Ordinal);
        Assert.Contains("INCLUDE ([Kills], [Deaths], [Outcome])", script, StringComparison.Ordinal);
        Assert.Contains("[PlayedAtUtc] DESC, [Id] DESC", script, StringComparison.Ordinal);
        Assert.Contains("ON DELETE NO ACTION", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Raw_queries_are_parameterized_without_connecting()
    {
        using var db = new DesignTimeDbContextFactory().CreateDbContext([]);
        var top = 20;
        var query = db.Database.SqlQuery<ShooterRecordsLab.Application.Rankings.RankingRow>($"EXEC dbo.GetTopPlayerRankings @Top={top}").ToQueryString();
        Assert.Contains("@Top=@p0", query, StringComparison.Ordinal);
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var read = db.Players.AsNoTracking().Where(p => p.Id == id).Select(p => new { p.Id, p.DisplayName }).ToQueryString();
        Assert.Contains("WHERE [p].[Id] = @", read, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatedAtUtc", read, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Server=server;Database=Db;Encrypt=False")]
    [InlineData("Server=server;Database=Db;TrustServerCertificate=True")]
    [InlineData("Server=server;Database=Db;MultipleActiveResultSets=True")]
    [InlineData("Server=server;Password=do-not-print")]
    [InlineData("invalid=do-not-print")]
    public void Invalid_database_configuration_is_rejected_without_echoing_secrets(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => DatabaseOptions.ValidateConnectionString(value));
        Assert.DoesNotContain("do-not-print", exception.ToString(), StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("1", null, null, null)]
    [InlineData("1", "Server=allowed;Database=LabTest", "other", "LabTest")]
    [InlineData("1", "Server=allowed;Database=LabDev", "allowed", "LabTest")]
    [InlineData("1", "Server=allowed;Database=LabDev", "allowed", "LabDev")]
    [InlineData("1", "Server=allowed;Database=LabTest;TrustServerCertificate=True", "allowed", "LabTest")]
    public void Database_test_guard_rejects_unapproved_or_wrong_targets_without_connecting(string? approved, string? raw, string? server, string? database)
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseTestSettings.FromValues(approved, raw, server, database));
    }

    [Fact]
    public void Unconfigured_or_verified_tls_configuration_is_valid()
    {
        DatabaseOptions.ValidateConnectionString(null);
        DatabaseOptions.ValidateConnectionString("Server=server;Database=Db;Encrypt=True;TrustServerCertificate=False");
    }
}

internal static class ModelExtensions
{
    public static IModel GetServiceModel(this ShooterRecordsDbContext db) =>
        Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(db).Model;
}
