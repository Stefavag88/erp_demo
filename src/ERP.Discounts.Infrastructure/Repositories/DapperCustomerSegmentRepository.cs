using Dapper;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Infrastructure.Data;
using System.Data;

namespace ERP.Discounts.Infrastructure.Repositories;

public sealed class DapperCustomerSegmentRepository(SqlConnectionFactory connections) : ICustomerSegmentRepository
{
    public async Task<string?> GetSegmentAsync(string customerId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "dbo.usp_CustomerDiscountProfile_GetSegment", 
                new { CustomerId = customerId }, 
                commandType: CommandType.StoredProcedure, 
                cancellationToken: cancellationToken));
    }
}
