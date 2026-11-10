using Microsoft.Data.SqlClient;

namespace ERP.CustomerCustomFields.Infrastructure.Data;

public sealed class SqlConnectionFactory(string connectionString)
{
    private readonly string connectionString = new SqlConnectionStringBuilder(connectionString)
    {
        ConnectRetryCount = 0
    }.ConnectionString;

    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
