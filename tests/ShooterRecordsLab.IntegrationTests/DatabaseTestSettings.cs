using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShooterRecordsLab.Infrastructure.Persistence;

namespace ShooterRecordsLab.IntegrationTests;

internal sealed class DatabaseTestSettings
{
    private readonly string connectionString;

    private DatabaseTestSettings(string connectionString) => this.connectionString = connectionString;

    public static DatabaseTestSettings LoadApproved()
    {
        return FromValues(Environment.GetEnvironmentVariable("SHOOTER_RECORDS_DATABASE_TESTS_APPROVED"),
            Environment.GetEnvironmentVariable("SHOOTER_RECORDS_TEST_CONNECTION"),
            Environment.GetEnvironmentVariable("SHOOTER_RECORDS_TEST_SERVER"),
            Environment.GetEnvironmentVariable("SHOOTER_RECORDS_TEST_DATABASE"));
    }

    internal static DatabaseTestSettings FromValues(string? approved, string? raw, string? server, string? database)
    {
        if (approved != "1")
        {
            throw new InvalidOperationException("Database tests require explicit approval and an exact target allowlist. They are not automatically skipped.");
        }

        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database) ||
            !database.EndsWith("Test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An approved test connection, exact server and Test database name must be configured outside source control.");
        }

        DatabaseOptions.ValidateConnectionString(raw);
        SqlConnectionStringBuilder parsed;
        try
        {
            parsed = new SqlConnectionStringBuilder(raw);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Invalid test database configuration.");
        }

        if (!string.Equals(parsed.DataSource, server, StringComparison.Ordinal) ||
            !string.Equals(parsed.InitialCatalog, database, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Test connection does not match the exact approved target.");
        }

        return new DatabaseTestSettings(raw);
    }

    public ShooterRecordsDbContext CreateContext() => new(new DbContextOptionsBuilder<ShooterRecordsDbContext>()
        .UseSqlServer(connectionString, sql => sql.UseCompatibilityLevel(160).CommandTimeout(30)).Options);
}
