using Npgsql;

namespace LogisticsTracker.Api.Data;

/// <summary>
/// Works out the PostgreSQL connection string from configuration.
/// Accepts either:
///   1. DATABASE_URL              - a postgres:// URI (what Neon and Supabase hand you), or
///   2. ConnectionStrings:Default - a standard Npgsql key/value string (used by docker-compose).
/// Nothing is hard-coded; secrets always come from the environment.
/// </summary>
public static class ConnectionStringResolver
{
    public static string Resolve(IConfiguration config)
    {
        var raw = config["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = config.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                "No database configured. Set DATABASE_URL (postgres:// URI) or ConnectionStrings__Default.");

        return raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
               raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? FromUri(raw)
            : raw;
    }

    public static string FromUri(string uriText)
    {
        var uri = new Uri(uriText);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = uri.AbsolutePath.Trim('/')
        };

        // Hosted providers (Neon, Supabase) require TLS. Local hosts do not.
        var isLocal = uri.Host is "localhost" or "127.0.0.1" or "db";
        if (!isLocal)
        {
            builder.SslMode = SslMode.Require;
            builder.TrustServerCertificate = true; // provider certs are not in the container's trust store
        }

        return builder.ConnectionString;
    }
}
