IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150630_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[Matches] (
        [Id] uniqueidentifier NOT NULL,
        [ExternalMatchId] nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [PlayedAtUtc] datetimeoffset(7) NOT NULL,
        [CreatedAtUtc] datetimeoffset(7) NOT NULL,
        [PayloadFingerprint] char(64) NOT NULL,
        CONSTRAINT [PK_Matches] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Matches_CreatedAtUtc] CHECK (DATEPART(TZOFFSET, [CreatedAtUtc]) = 0),
        CONSTRAINT [CK_Matches_ExternalMatchId] CHECK (DATALENGTH([ExternalMatchId]) BETWEEN 2 AND 128 AND [ExternalMatchId] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_-]%'),
        CONSTRAINT [CK_Matches_Fingerprint] CHECK (DATALENGTH([PayloadFingerprint]) = 64 AND [PayloadFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-F0-9]%'),
        CONSTRAINT [CK_Matches_PlayedAtUtc] CHECK (DATEPART(TZOFFSET, [PlayedAtUtc]) = 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150630_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[Players] (
        [Id] uniqueidentifier NOT NULL,
        [DisplayName] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetimeoffset(7) NOT NULL,
        CONSTRAINT [PK_Players] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Players_CreatedAtUtc] CHECK (DATEPART(TZOFFSET, [CreatedAtUtc]) = 0),
        CONSTRAINT [CK_Players_DisplayName] CHECK (LEN(LTRIM(RTRIM([DisplayName]))) > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150630_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[MatchResults] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] uniqueidentifier NOT NULL,
        [PlayerId] uniqueidentifier NOT NULL,
        [Kills] int NOT NULL,
        [Deaths] int NOT NULL,
        [Outcome] int NOT NULL,
        CONSTRAINT [PK_MatchResults] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MatchResults_Deaths] CHECK ([Deaths] BETWEEN 0 AND 10000),
        CONSTRAINT [CK_MatchResults_Kills] CHECK ([Kills] BETWEEN 0 AND 10000),
        CONSTRAINT [CK_MatchResults_Outcome] CHECK ([Outcome] IN (0, 1, 2)),
        CONSTRAINT [FK_MatchResults_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [dbo].[Matches] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MatchResults_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [dbo].[Players] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150630_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Matches_ExternalMatchId] ON [dbo].[Matches] ([ExternalMatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150630_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_PlayedAtUtc_Id] ON [dbo].[Matches] ([PlayedAtUtc] DESC, [Id] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150630_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchResults_MatchId_PlayerId] ON [dbo].[MatchResults] ([MatchId], [PlayerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150630_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchResults_PlayerId_MatchId] ON [dbo].[MatchResults] ([PlayerId], [MatchId]) INCLUDE ([Kills], [Deaths], [Outcome]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150630_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022150630_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150634_AddRankingsProcedure'
)
BEGIN
    EXEC(N'CREATE OR ALTER PROCEDURE dbo.GetTopPlayerRankings
        @Top int
    AS
    BEGIN
        SET NOCOUNT ON;
        IF @Top IS NULL OR @Top < 1 OR @Top > 100
            THROW 50001, ''Top must be between 1 and 100.'', 1;

        WITH Totals AS
        (
            SELECT p.Id AS PlayerId, p.DisplayName,
                   COUNT_BIG(*) AS Matches,
                   SUM(CASE WHEN r.Outcome = 0 THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS Wins,
                   SUM(CAST(r.Kills AS bigint)) AS Kills
            FROM dbo.Players AS p
            INNER JOIN dbo.MatchResults AS r ON r.PlayerId = p.Id
            GROUP BY p.Id, p.DisplayName
        )
        SELECT TOP (@Top)
               ROW_NUMBER() OVER (ORDER BY Wins DESC, Kills DESC, PlayerId ASC) AS Rank,
               PlayerId, DisplayName, Matches, Wins, Kills
        FROM Totals
        ORDER BY Wins DESC, Kills DESC, PlayerId ASC;
    END;
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022150634_AddRankingsProcedure'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022150634_AddRankingsProcedure', N'10.0.13');
END;

COMMIT;
GO
