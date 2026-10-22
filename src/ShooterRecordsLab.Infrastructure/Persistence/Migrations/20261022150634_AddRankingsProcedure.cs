using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShooterRecordsLab.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRankingsProcedure : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        using var stream = typeof(AddRankingsProcedure).Assembly.GetManifestResourceStream(
            "ShooterRecordsLab.Infrastructure.Persistence.Sql.V001_GetTopPlayerRankings.sql")
            ?? throw new InvalidOperationException("Versioned ranking SQL is missing.");
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd().Replace("'", "''", StringComparison.Ordinal);
        migrationBuilder.Sql("EXEC(N'" + sql + "')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.GetTopPlayerRankings;");
    }
}
