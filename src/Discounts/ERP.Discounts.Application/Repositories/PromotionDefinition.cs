namespace ERP.Discounts.Application.Repositories;

public sealed record PromotionDefinition(
    Guid Id, 
    string Name, 
    decimal MinimumCurrentAmount,
    int PriorityWithinType, 
    bool IsActive, 
    DateTimeOffset? StartsAtUtc, 
    DateTimeOffset? EndsAtUtc, 
    IReadOnlyList<PromotionSegmentRate> SegmentRates);
