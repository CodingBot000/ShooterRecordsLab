using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShooterRecordsLab.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class FixExternalMatchIdCharacterSet : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Matches_ExternalMatchId",
            schema: "dbo",
            table: "Matches");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Matches_ExternalMatchId",
            schema: "dbo",
            table: "Matches",
            sql: "DATALENGTH([ExternalMatchId]) BETWEEN 2 AND 128 AND [ExternalMatchId] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^-A-Z0-9_]%'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Matches_ExternalMatchId",
            schema: "dbo",
            table: "Matches");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Matches_ExternalMatchId",
            schema: "dbo",
            table: "Matches",
            sql: "DATALENGTH([ExternalMatchId]) BETWEEN 2 AND 128 AND [ExternalMatchId] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_-]%'");
    }
}
