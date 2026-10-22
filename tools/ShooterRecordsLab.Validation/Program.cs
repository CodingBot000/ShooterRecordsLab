using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShooterRecordsLab.Domain.Entities;
using ShooterRecordsLab.Infrastructure.Persistence;

try
{
    if (args.Length != 1 || args[0] is not ("prepare" or "performance") || Environment.GetEnvironmentVariable("SHOOTER_RECORDS_VALIDATION_APPROVED") != "1")
    {
        throw new InvalidOperationException("Explicit validation approval and prepare command are required.");
    }

    var connectionString = Required("VALIDATION_ADMIN_CONNECTION");
    DatabaseOptions.ValidateConnectionString(connectionString);
    var connection = new SqlConnectionStringBuilder(connectionString);
    if (connection.InitialCatalog != "master" || connection.DataSource != Required("VALIDATION_EXPECTED_SERVER"))
    {
        throw new InvalidOperationException("Unexpected validation target.");
    }

    using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(8));
    var token = cancellation.Token;
    await using var master = new SqlConnection(connectionString);
    await master.OpenAsync(token);
    await using (var version = new SqlCommand("SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)", master))
    {
        if ((int)(await version.ExecuteScalarAsync(token))! != 16)
        {
            throw new InvalidOperationException("Expected SQL Server 2022.");
        }
    }

    await using (var tls = new SqlCommand("SELECT encrypt_option FROM sys.dm_exec_connections WHERE session_id = @@SPID", master))
    {
        if (!string.Equals((string?)await tls.ExecuteScalarAsync(token), "TRUE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQL connection is not encrypted.");
        }
    }

    if (args[0] == "performance")
    {
        await master.ChangeDatabaseAsync("ShooterRecordsLabDev", token);
        await RunPerformance(master, token);
        return;
    }

    await Execute(master, "IF DB_ID(N'ShooterRecordsLabDev') IS NULL CREATE DATABASE [ShooterRecordsLabDev]", token);
    await Execute(master, "IF DB_ID(N'ShooterRecordsLabTest') IS NULL CREATE DATABASE [ShooterRecordsLabTest]", token);
    await CreateLogin(master, "srl_api", Required("VALIDATION_APP_PASSWORD"), token);
    await CreateLogin(master, "srl_test", Required("VALIDATION_TEST_PASSWORD"), token);

    foreach (var database in new[] { "ShooterRecordsLabDev", "ShooterRecordsLabTest" })
    {
        connection.InitialCatalog = database;
        await using var db = new ShooterRecordsDbContext(new DbContextOptionsBuilder<ShooterRecordsDbContext>()
            .UseSqlServer(connection.ConnectionString, sql => sql.UseCompatibilityLevel(160).CommandTimeout(60)).Options);
        await db.Database.MigrateAsync(token);
        await using var admin = new SqlConnection(connection.ConnectionString);
        await admin.OpenAsync(token);
        var login = database == "ShooterRecordsLabDev" ? "srl_api" : "srl_test";
        await Execute(admin, $"IF USER_ID(N'{login}') IS NULL CREATE USER [{login}] FOR LOGIN [{login}]", token);
        foreach (var table in new[] { "Players", "Matches", "MatchResults", "__EFMigrationsHistory" })
        {
            await Execute(admin, $"GRANT SELECT ON OBJECT::dbo.[{table}] TO [{login}]", token);
        }

        await Execute(admin, $"GRANT EXECUTE ON OBJECT::dbo.GetTopPlayerRankings TO [{login}]", token);
        foreach (var table in database == "ShooterRecordsLabDev" ? new[] { "Matches", "MatchResults" } : new[] { "Players", "Matches", "MatchResults" })
        {
            await Execute(admin, $"GRANT INSERT ON OBJECT::dbo.[{table}] TO [{login}]", token);
            if (database == "ShooterRecordsLabTest")
            {
                await Execute(admin, $"GRANT UPDATE, DELETE ON OBJECT::dbo.[{table}] TO [{login}]", token);
            }
        }

        if (database == "ShooterRecordsLabDev")
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var (id, name) in new[]
            {
                (Guid.Parse("11111111-1111-1111-1111-111111111111"), "PracticePlayerA"),
                (Guid.Parse("22222222-2222-2222-2222-222222222222"), "PracticePlayerB")
            })
            {
                if (!await db.Players.AnyAsync(p => p.Id == id, token))
                {
                    db.Players.Add(new Player(id, name, now));
                }
            }

            await db.SaveChangesAsync(token);
        }
    }

    Console.WriteLine(JsonSerializer.Serialize(new { prepared = true, databaseCount = 2, tlsVerified = true, minimumPrivilegeUsers = true, migrationApplied = true }));
}
catch (Exception exception)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new
    {
        result = "FAIL",
        errorType = exception.GetType().Name,
        sqlErrorNumber = exception is SqlException sql ? (int?)sql.Number : null
    }));
    Environment.ExitCode = 1;
}

static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
    ? value : throw new InvalidOperationException("Required private validation input is missing.");

static async Task Execute(SqlConnection connection, string sql, CancellationToken token)
{
    await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
    await command.ExecuteNonQueryAsync(token);
}

static async Task CreateLogin(SqlConnection connection, string name, string password, CancellationToken token)
{
    // name comes only from constants above; the secret is a parameter and never printed.
    await using var command = new SqlCommand($"""
        IF SUSER_ID(N'{name}') IS NULL
        BEGIN
            DECLARE @command nvarchar(max) = N'CREATE LOGIN [{name}] WITH PASSWORD = N''' + REPLACE(@password, '''', '''''') + N''', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF';
            EXEC(@command);
        END
        """, connection);
    command.Parameters.Add("@password", SqlDbType.NVarChar, 128).Value = password;
    await command.ExecuteNonQueryAsync(token);
}


static async Task RunPerformance(SqlConnection connection, CancellationToken token)
{
    var players = new[] { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") };
    var matches = new DataTable();
    foreach (var (name, type) in new[] { ("Id", typeof(Guid)), ("ExternalMatchId", typeof(string)), ("PlayedAtUtc", typeof(DateTimeOffset)), ("CreatedAtUtc", typeof(DateTimeOffset)), ("PayloadFingerprint", typeof(string)) })
    {
        matches.Columns.Add(name, type);
    }

    var results = new DataTable();
    foreach (var (name, type) in new[] { ("Id", typeof(Guid)), ("MatchId", typeof(Guid)), ("PlayerId", typeof(Guid)), ("Kills", typeof(int)), ("Deaths", typeof(int)), ("Outcome", typeof(int)) })
    {
        results.Columns.Add(name, type);
    }

    var now = DateTimeOffset.UtcNow;
    for (var i = 0; i < 5000; i++)
    {
        var id = Guid.NewGuid();
        matches.Rows.Add(id, "PERF-" + id.ToString("N").ToUpperInvariant(), now.AddMinutes(-i), now, new string('A', 64));
        results.Rows.Add(Guid.NewGuid(), id, players[0], i % 20, i % 10, 0);
        results.Rows.Add(Guid.NewGuid(), id, players[1], i % 10, i % 20, 1);
    }

    await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(token);
    try
    {
        foreach (var (table, data) in new[] { ("dbo.Matches", matches), ("dbo.MatchResults", results) })
        {
            using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints, transaction) { DestinationTableName = table, BulkCopyTimeout = 120 };
            foreach (DataColumn column in data.Columns)
            {
                bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulk.WriteToServerAsync(data, token);
        }

        var metrics = new List<object>();
        foreach (var (name, sql) in new[]
        {
            ("query-plan-matches", "SELECT m.Id, m.PlayedAtUtc, r.Kills, r.Deaths, r.Outcome FROM dbo.MatchResults r JOIN dbo.Matches m ON m.Id=r.MatchId WHERE r.PlayerId=@PlayerId ORDER BY m.PlayedAtUtc DESC,m.Id DESC OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY"),
            ("query-plan-statistics", "SELECT COUNT_BIG(*) AS Matches,SUM(CAST(Kills AS bigint)) AS Kills,SUM(CAST(Deaths AS bigint)) AS Deaths FROM dbo.MatchResults WHERE PlayerId=@PlayerId"),
            ("query-plan-rankings", "EXEC dbo.GetTopPlayerRankings @Top=20")
        })
        {
            long reads = 0;
            SqlInfoMessageEventHandler handler = (_, info) =>
            {
                foreach (System.Text.RegularExpressions.Match found in Regex.Matches(info.Message, @"logical reads (\d+)", RegexOptions.CultureInvariant))
                {
                    reads += long.Parse(found.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                }
            };
            connection.InfoMessage += handler;
            var timer = Stopwatch.StartNew();
            var rows = 0;
            var operators = 0;
            var actualCounters = 0;
            try
            {
                await using var command = new SqlCommand("SET STATISTICS IO ON; SET STATISTICS XML ON; " + sql + "; SET STATISTICS XML OFF; SET STATISTICS IO OFF;", connection, transaction) { CommandTimeout = 90 };
                command.Parameters.Add("@PlayerId", SqlDbType.UniqueIdentifier).Value = players[0];
                await using var reader = await command.ExecuteReaderAsync(token);
                do
                {
                    while (await reader.ReadAsync(token))
                    {
                        if (reader.FieldCount == 1 && reader.GetDataTypeName(0) == "xml")
                        {
                            var xml = XDocument.Parse(reader.GetSqlXml(0).Value);
                            operators += xml.Descendants().Count(n => n.Name.LocalName == "RelOp");
                            actualCounters += xml.Descendants().Count(n => n.Name.LocalName == "RunTimeCountersPerThread");
                        }
                        else
                        {
                            rows++;
                        }
                    }
                } while (await reader.NextResultAsync(token));
            }
            finally
            {
                connection.InfoMessage -= handler;
            }

            if (operators == 0 || actualCounters == 0)
            {
                throw new InvalidOperationException("Actual execution plan was not captured.");
            }

            metrics.Add(new { name, result = "PASS", sampleRows = 10000, returnedRows = rows, logicalReads = reads, planOperators = operators, durationMs = Math.Round(timer.Elapsed.TotalMilliseconds, 2) });
        }

        Console.WriteLine(JsonSerializer.Serialize(new { performanceVerified = true, metrics }));
    }
    finally
    {
        await transaction.RollbackAsync(CancellationToken.None);
    }
}
