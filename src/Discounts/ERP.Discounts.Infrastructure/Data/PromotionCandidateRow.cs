using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Infrastructure.Data;

internal sealed class PromotionCandidateRow
{
    public Guid PromotionId { get; set; }
    public string Name { get; set; } = "";
    public decimal MinimumCurrentAmount { get; set; }
    public int PriorityWithinType { get; set; }
    public bool IsActive { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public string SegmentCode { get; set; } = "";
    public byte CalculationMethod { get; set; }
    public byte CalculationBase { get; set; }
    public decimal Value { get; set; }

    internal PromotionCandidate ToCandidate()
    {
        var discount = new Discount(PromotionId.ToString("D"), Name, DiscountType.Promotion,
            DiscountRowMapper.Method(CalculationMethod), Value, PriorityWithinType, DiscountRowMapper.Base(CalculationBase));

        return new (
            discount,
            SegmentCode,
            MinimumCurrentAmount,
            IsActive,
            DiscountRowMapper.Utc(StartsAtUtc),
            DiscountRowMapper.Utc(EndsAtUtc));
    }
}
