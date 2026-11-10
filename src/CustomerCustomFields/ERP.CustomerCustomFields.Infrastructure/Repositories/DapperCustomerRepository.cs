using Dapper;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Domain.Models;
using ERP.CustomerCustomFields.Infrastructure.Data;
using System.Data;

namespace ERP.CustomerCustomFields.Infrastructure.Repositories;

public sealed class DapperCustomerRepository(SqlConnectionFactory connections) : ICustomerRepository
{
    public async Task<IReadOnlyList<Customer>> ListAsync(CancellationToken token)
    {
        await using var c = await connections.OpenAsync(token);

        return (await c.QueryAsync<CustomerRow>(new CommandDefinition(
            "dbo.Customer_List", 
            commandType: CommandType.StoredProcedure, 
            cancellationToken: token)))
            .Select(row => row.ToCustomer())
            .ToArray()
            .AsReadOnly();
    }

    public async Task<Customer> CreateAsync(Guid customerId, string name, CancellationToken token)
    {
        await using var c = await connections.OpenAsync(token);

        var args = new DynamicParameters();
        args.Add("CustomerId", customerId, DbType.Guid);
        args.Add("Name", name, DbType.String, size: -1);

        var row = await c.QuerySingleAsync<CustomerRow>(new CommandDefinition(
            "dbo.Customer_Create", 
            args, 
            commandType: CommandType.StoredProcedure, 
            cancellationToken: token));

        return row.ToCustomer();
    }
}
