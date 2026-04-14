using Microsoft.Data.SqlClient;

namespace AppModAssist.Services;

public class SqlConnectionFactory
{
    private readonly IConfiguration _configuration;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SqlConnection Create()
    {
        var connectionString = _configuration.GetConnectionString("SqlDatabase")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:SqlDatabase.");

        var managedIdentityClientId = _configuration["ManagedIdentityClientId"] ?? _configuration["AZURE_CLIENT_ID"];
        var builder = new SqlConnectionStringBuilder(connectionString);

        if (!string.IsNullOrWhiteSpace(managedIdentityClientId) && string.IsNullOrWhiteSpace(builder.UserID))
        {
            builder.UserID = managedIdentityClientId;
        }

        return new SqlConnection(builder.ConnectionString);
    }
}
