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
        var parameters = new DynamicParameters();
        parameters.Add("CustomerId", customerId, DbType.String, size: 64);
        parameters.Add("AsOfUtc", asOfUtc.UtcDateTime, DbType.DateTime2);
        var rows = await connection.QueryAsync<PriceListRow>(new CommandDefinition("dbo.usp_PriceList_GetEligible", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return Array.AsReadOnly(rows.Select(r => r.ToDefinition()).ToArray());
    }
}
