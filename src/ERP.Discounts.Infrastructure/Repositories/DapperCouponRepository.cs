using Dapper;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Infrastructure.Data;
using System.Data;

namespace ERP.Discounts.Infrastructure.Repositories;

public sealed class DapperCouponRepository(SqlConnectionFactory connections) : ICouponRepository
{
    public async Task<IReadOnlyList<CouponDefinition>> GetEligibleAsync(string customerId, IReadOnlyList<string> codes, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        using var table = CouponCodeTable.Create(codes);
        await using var connection = await connections.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("CustomerId", customerId, DbType.String, size: 64);
        parameters.Add("AsOfUtc", asOfUtc.UtcDateTime, DbType.DateTime2);
        parameters.Add("Codes", table.AsTableValuedParameter("dbo.CouponCodeList"));

        var rows = await connection.QueryAsync<CouponRow>(
            new CommandDefinition(
                "dbo.usp_Coupon_GetEligible", 
                parameters, 
                commandType: CommandType.StoredProcedure, 
                cancellationToken: cancellationToken));

        return rows
            .Select(r => r.ToDefinition())
            .ToArray()
            .AsReadOnly();
    }
}
