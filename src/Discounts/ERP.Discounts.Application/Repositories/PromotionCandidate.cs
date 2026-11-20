using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application.Repositories;

public sealed record PromotionCandidate(
    Discount Discount,
    string SegmentCode,
    decimal MinimumCurrentAmount,
    bool IsActive,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc);
