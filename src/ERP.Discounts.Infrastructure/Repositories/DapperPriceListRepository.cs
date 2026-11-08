using Dapper;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Resilience;
using System.Data;

namespace ERP.Discounts.Infrastructure.Repositories;

public sealed class DapperPriceListRepository(SqlConnectionFactory connections, SqlReadRetryPolicy retry) : IPriceListRepository
{
    public Task<IReadOnlyList<PriceListDefinition>> GetEligibleAsync(string customerId, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
        => retry.ExecuteAsync<IReadOnlyList<PriceListDefinition>>(async token =>
        {
            await using var connection = await connections.OpenAsync(token);

            var parameters = new DynamicParameters();
            parameters.Add("CustomerId", customerId, DbType.String, size: 64);
            parameters.Add("AsOfUtc", asOfUtc.UtcDateTime, DbType.DateTime2);

            var rows = await connection.QueryAsync<PriceListRow>(
                new CommandDefinition(
                    "dbo.usp_PriceList_GetEligible",
                    parameters, commandType: CommandType.StoredProcedure,
                    cancellationToken: token));

            return rows
                .Select(r => r.ToDefinition())
                .ToArray()
                .AsReadOnly();
        }, cancellationToken);
}
