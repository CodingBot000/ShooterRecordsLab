CREATE OR ALTER PROCEDURE dbo.GetTopPlayerRankings
    @Top int
AS
BEGIN
    SET NOCOUNT ON;
    IF @Top IS NULL OR @Top < 1 OR @Top > 100
        THROW 50001, 'Top must be between 1 and 100.', 1;

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
