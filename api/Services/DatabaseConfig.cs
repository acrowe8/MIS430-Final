namespace api.Services;

/// <summary>
/// Reads JAWSDB_URL (mysql://user:password@host:port/database) and builds a MySqlConnector string.
/// </summary>
public static class DatabaseConfig
{
    public static string GetConnectionString()
    {
        var url = Environment.GetEnvironmentVariable("JAWSDB_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                "JAWSDB_URL is not set. Add it to .env (local) or Heroku config vars.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "mysql")
        {
            throw new InvalidOperationException(
                "JAWSDB_URL must be a mysql://user:password@host:port/database URL.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');

        if (string.IsNullOrEmpty(database))
        {
            throw new InvalidOperationException("JAWSDB_URL is missing the database name.");
        }

        var port = uri.Port > 0 ? uri.Port : 3306;

        return $"Server={uri.Host};Port={port};Database={database};User Id={user};Password={password};SslMode=Required;";
    }
}
