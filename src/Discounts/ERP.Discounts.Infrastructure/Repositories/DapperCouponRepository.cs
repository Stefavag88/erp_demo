using Dapper;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Resilience;
using System.Data;

namespace ERP.Discounts.Infrastructure.Repositories;

public sealed class DapperCouponRepository(SqlConnectionFactory connections, SqlReadRetryPolicy retry) : ICouponRepository
{
    public Task<IReadOnlyList<CouponDefinition>> GetEligibleAsync(string customerId, IReadOnlyList<string> codes, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
        => retry.ExecuteAsync<IReadOnlyList<CouponDefinition>>(async token =>
        {
            using var table = CouponCodeTable.Create(codes);
            await using var connection = await connections.OpenAsync(token);

            var parameters = new DynamicParameters();
            parameters.Add("CustomerId", customerId, DbType.String, size: 64);
            parameters.Add("AsOfUtc", asOfUtc.UtcDateTime, DbType.DateTime2);
            parameters.Add("Codes", table.AsTableValuedParameter("dbo.CouponCodeList"));

            var rows = await connection.QueryAsync<CouponRow>(
                new CommandDefinition(
                    "dbo.usp_Coupon_GetEligible",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: token));

            return rows
                .Select(r => r.ToDefinition())
                .ToArray()
                .AsReadOnly();

        }, cancellationToken);
}
