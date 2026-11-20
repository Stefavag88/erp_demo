using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application.Repositories;

public sealed record PromotionCandidate(
    ResolvedDiscount Discount,
    string SegmentCode,
    decimal MinimumCurrentAmount,
    bool IsActive,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc);
