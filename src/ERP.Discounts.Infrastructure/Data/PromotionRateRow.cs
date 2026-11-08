using ERP.Discounts.Application.Repositories;
namespace ERP.Discounts.Infrastructure.Data;
internal sealed class PromotionRateRow
{
    public Guid PromotionId { get; set; }
    public string SegmentCode { get; set; } = "";
    public string CalculationMethod { get; set; } = "";
    public decimal Value { get; set; }
    internal PromotionSegmentRate ToDefinition() => new(SegmentCode,DiscountRowMapper.Method(CalculationMethod),Value);
}
