using Dapper;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Infrastructure.Data;
using System.Data;

namespace ERP.Discounts.Infrastructure.Repositories;

public sealed class DapperPromotionRepository(SqlConnectionFactory connections) : IPromotionRepository
{
    public async Task<IReadOnlyList<PromotionDefinition>> GetActiveDefinitionsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("AsOfUtc", asOfUtc.UtcDateTime, DbType.DateTime2);

        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(
                "dbo.usp_Promotion_GetActiveDefinitions", 
                parameters, commandType: CommandType.StoredProcedure, 
                cancellationToken: cancellationToken));

        var definitions = (await results.ReadAsync<PromotionRow>()).ToArray();
        var rates = (await results.ReadAsync<PromotionRateRow>()).ToArray();

        return PromotionRows.Map(definitions, rates);
    }
}
