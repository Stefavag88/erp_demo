using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Infrastructure.Data;

internal sealed class CouponRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public byte CalculationMethod { get; set; }
    public byte CalculationBase { get; set; }
    public decimal Value { get; set; }
    public int PriorityWithinType { get; set; }

    internal ResolvedDiscount ToDiscount()
        => new(Id.ToString("D"), Name, DiscountType.Coupon, DiscountRowMapper.Method(CalculationMethod), Value, PriorityWithinType, DiscountRowMapper.Base(CalculationBase));
}
