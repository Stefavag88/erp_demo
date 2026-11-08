using ERP.Discounts.Application.Validation;
using System.Data;

namespace ERP.Discounts.Infrastructure.Data;

internal static class CouponCodeTable
{
    internal static DataTable Create(IReadOnlyList<string> codes)
    {
        var normalized = CouponCodeNormalizer.Normalize(codes);

        var table = new DataTable();
        var column = table.Columns.Add("Code", typeof(string));
        column.MaxLength = 64; column.AllowDBNull = false;

        foreach (var code in normalized) 
            table.Rows.Add(code);

        return table;
    }
}
