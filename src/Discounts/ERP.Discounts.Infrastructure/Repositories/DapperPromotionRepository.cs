using Dapper;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Resilience;
using System.Data;

namespace ERP.Discounts.Infrastructure.Repositories;

public sealed class DapperPromotionRepository(SqlConnectionFactory connections, SqlReadRetryPolicy retry) : IPromotionRepository
{
    public Task<IReadOnlyList<PromotionDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
        => retry.ExecuteAsync<IReadOnlyList<PromotionDefinition>>(async token =>
        {
            await using var connection = await connections.OpenAsync(token);

            using var results = await connection.QueryMultipleAsync(
                new CommandDefinition(
                    "dbo.usp_Promotion_GetDefinitions",
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: token));

            var definitions = (await results.ReadAsync<PromotionRow>()).ToArray();
            var rates = (await results.ReadAsync<PromotionRateRow>()).ToArray();

            return PromotionRows.Map(definitions, rates);
        }, cancellationToken);
}
