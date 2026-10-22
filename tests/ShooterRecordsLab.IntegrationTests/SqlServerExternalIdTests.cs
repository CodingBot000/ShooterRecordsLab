using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ShooterRecordsLab.IntegrationTests;

[Trait("Category", "Database")]
public sealed class SqlServerExternalIdTests
{
    [Theory]
    [InlineData("VALID-ID", true)]
    [InlineData("VALID_ID", true)]
    [InlineData("-", true)]
    [InlineData("_", true)]
    [InlineData("A0_Z-9", true)]
    [InlineData("lowercase", false)]
    [InlineData("HAS SPACE", false)]
    [InlineData("HAS%PERCENT", false)]
    [InlineData("é", false)]
    [InlineData("ſ", false)]
    [InlineData("ABC/DEF", false)]
    public async Task Database_external_id_constraint_matches_canonical_ASCII_contract(string externalId, bool valid)
    {
        var settings = DatabaseTestSettings.LoadApproved();
        await using var db = settings.CreateContext();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var fingerprint = new string('A', 64);
        try
        {
            Task<int> Insert() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO dbo.Matches (Id, ExternalMatchId, PlayedAtUtc, CreatedAtUtc, PayloadFingerprint)
                VALUES ({id}, {externalId}, {now}, {now}, {fingerprint})
                """, CancellationToken.None);
            if (valid)
            {
                Assert.Equal(1, await Insert());
            }
            else
            {
                var error = await Assert.ThrowsAsync<SqlException>(Insert);
                Assert.Equal(547, error.Number);
            }
        }
        finally
        {
            await db.Matches.Where(match => match.Id == id).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
}
