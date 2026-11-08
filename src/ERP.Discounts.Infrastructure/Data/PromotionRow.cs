using ERP.Discounts.Application.Repositories;
namespace ERP.Discounts.Infrastructure.Data;
internal sealed class PromotionRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string CalculationBase { get; set; } = "";
    public decimal MinimumCurrentAmount { get; set; }
    public int PriorityWithinType { get; set; }
    public bool IsActive { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    internal PromotionDefinition ToDefinition(IReadOnlyList<PromotionSegmentRate> rates) => new(Id,Name,DiscountRowMapper.Base(CalculationBase),MinimumCurrentAmount,PriorityWithinType,IsActive,DiscountRowMapper.Utc(StartsAtUtc),DiscountRowMapper.Utc(EndsAtUtc),rates);
}
