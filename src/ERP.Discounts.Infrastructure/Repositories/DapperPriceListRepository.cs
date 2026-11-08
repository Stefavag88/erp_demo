using System.Data;
using Dapper;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Infrastructure.Data;
namespace ERP.Discounts.Infrastructure.Repositories;
public sealed class DapperPriceListRepository(SqlConnectionFactory connections) : IPriceListRepository
{
    public async Task<IReadOnlyList<PriceListDefinition>> GetEligibleAsync(string customerId, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<PriceListRow>(new CommandDefinition("dbo.usp_PriceList_GetEligible", new { CustomerId = customerId, AsOfUtc = asOfUtc.UtcDateTime }, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return Array.AsReadOnly(rows.Select(r => r.ToDefinition()).ToArray());
    }
}
