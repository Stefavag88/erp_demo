using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Infrastructure.Data;

internal sealed class PromotionRateRow
{
    public Guid PromotionId { get; set; }
    public string SegmentCode { get; set; } = "";
    public byte CalculationMethod { get; set; }
    public byte CalculationBase { get; set; }
    public decimal Value { get; set; }

    internal PromotionCandidate ToCandidate(PromotionRow promotion)
        => new(
            new ResolvedDiscount(promotion.Id.ToString("D"), promotion.Name, DiscountType.Promotion,
                DiscountRowMapper.Method(CalculationMethod), Value, promotion.PriorityWithinType, DiscountRowMapper.Base(CalculationBase)),
            SegmentCode, promotion.MinimumCurrentAmount, promotion.IsActive,
            DiscountRowMapper.Utc(promotion.StartsAtUtc), DiscountRowMapper.Utc(promotion.EndsAtUtc));
}
