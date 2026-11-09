using System.Data;
using Dapper;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Domain.Models;
using ERP.CustomerCustomFields.Infrastructure.Data;

namespace ERP.CustomerCustomFields.Infrastructure.Repositories;

public sealed class DapperCustomerRepository(string connectionString) : ICustomerRepository
{
    private readonly SqlConnectionFactory connections = new(connectionString);

    public async Task<IReadOnlyList<Customer>> ListAsync(CancellationToken token)
    {
        using var c = connections.Create();
        await c.OpenAsync(token);
        return (await c.QueryAsync<Customer>(new CommandDefinition("dbo.Customer_List", commandType: CommandType.StoredProcedure, cancellationToken: token))).ToArray();
    }

    public async Task<Customer> CreateAsync(Guid customerId, string name, CancellationToken token)
    {
        using var c = connections.Create();
        await c.OpenAsync(token);
        var args = new DynamicParameters();
        args.Add("CustomerId", customerId, DbType.Guid);
        args.Add("Name", name, DbType.String, size: -1);
        return await c.QuerySingleAsync<Customer>(new CommandDefinition("dbo.Customer_Create", args, commandType: CommandType.StoredProcedure, cancellationToken: token));
    }
}
