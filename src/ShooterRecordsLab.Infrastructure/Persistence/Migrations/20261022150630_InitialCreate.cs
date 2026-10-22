using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShooterRecordsLab.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "dbo");

        migrationBuilder.CreateTable(
            name: "Matches",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExternalMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                PlayedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                PayloadFingerprint = table.Column<string>(type: "char(64)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Matches", x => x.Id);
                table.CheckConstraint("CK_Matches_CreatedAtUtc", "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                table.CheckConstraint("CK_Matches_ExternalMatchId", "DATALENGTH([ExternalMatchId]) BETWEEN 2 AND 128 AND [ExternalMatchId] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_-]%'");
                table.CheckConstraint("CK_Matches_Fingerprint", "DATALENGTH([PayloadFingerprint]) = 64 AND [PayloadFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-F0-9]%'");
                table.CheckConstraint("CK_Matches_PlayedAtUtc", "DATEPART(TZOFFSET, [PlayedAtUtc]) = 0");
            });

        migrationBuilder.CreateTable(
            name: "Players",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Players", x => x.Id);
                table.CheckConstraint("CK_Players_CreatedAtUtc", "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                table.CheckConstraint("CK_Players_DisplayName", "LEN(LTRIM(RTRIM([DisplayName]))) > 0");
            });

        migrationBuilder.CreateTable(
            name: "MatchResults",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kills = table.Column<int>(type: "int", nullable: false),
                Deaths = table.Column<int>(type: "int", nullable: false),
                Outcome = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MatchResults", x => x.Id);
                table.CheckConstraint("CK_MatchResults_Deaths", "[Deaths] BETWEEN 0 AND 10000");
                table.CheckConstraint("CK_MatchResults_Kills", "[Kills] BETWEEN 0 AND 10000");
                table.CheckConstraint("CK_MatchResults_Outcome", "[Outcome] IN (0, 1, 2)");
                table.ForeignKey(
                    name: "FK_MatchResults_Matches_MatchId",
                    column: x => x.MatchId,
                    principalSchema: "dbo",
                    principalTable: "Matches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MatchResults_Players_PlayerId",
                    column: x => x.PlayerId,
                    principalSchema: "dbo",
                    principalTable: "Players",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Matches_ExternalMatchId",
            schema: "dbo",
            table: "Matches",
            column: "ExternalMatchId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Matches_PlayedAtUtc_Id",
            schema: "dbo",
            table: "Matches",
            columns: new[] { "PlayedAtUtc", "Id" },
            descending: new bool[0]);

        migrationBuilder.CreateIndex(
            name: "IX_MatchResults_MatchId_PlayerId",
            schema: "dbo",
            table: "MatchResults",
            columns: new[] { "MatchId", "PlayerId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MatchResults_PlayerId_MatchId",
            schema: "dbo",
            table: "MatchResults",
            columns: new[] { "PlayerId", "MatchId" })
            .Annotation("SqlServer:Include", new[] { "Kills", "Deaths", "Outcome" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MatchResults",
            schema: "dbo");

        migrationBuilder.DropTable(
            name: "Matches",
            schema: "dbo");

        migrationBuilder.DropTable(
            name: "Players",
            schema: "dbo");
    }
}
