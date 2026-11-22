using Dapper;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Resilience;
using System.Data;

namespace ERP.Discounts.Infrastructure.Repositories;

public sealed class DapperPromotionRepository(SqlConnectionFactory connections, SqlReadRetryPolicy retry) : IPromotionRepository
{
    public Task<IReadOnlyList<PromotionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken)
        => retry.ExecuteAsync<IReadOnlyList<PromotionCandidate>>(async token =>
        {
            await using var connection = await connections.OpenAsync(token);

            var rows = await connection.QueryAsync<PromotionCandidateRow>(
                new CommandDefinition(
                    "dbo.usp_Promotion_GetDefinitions",
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: token));

            return rows.Select(row => row.ToCandidate()).ToArray().AsReadOnly();
        }, cancellationToken);
}
