using Microsoft.Data.SqlClient;

namespace ShooterRecordsLab.Infrastructure.Persistence;

public static class DatabaseOptions
{
    public static void ValidateConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        try
        {
            var parsed = new SqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog) ||
                parsed.TrustServerCertificate || parsed.Encrypt == SqlConnectionEncryptOption.Optional || parsed.MultipleActiveResultSets)
            {
                throw new ArgumentException();
            }
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Database configuration requires a server, database, verified TLS and MARS disabled.");
        }
    }
}
