using Microsoft.Data.SqlClient;

namespace ERP.CustomerCustomFields.Infrastructure.Data;

public sealed class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(new SqlConnectionStringBuilder(connectionString) { ConnectRetryCount = 0 }.ConnectionString);
}
