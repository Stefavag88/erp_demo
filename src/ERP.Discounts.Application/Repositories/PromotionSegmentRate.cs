using ERP.Discounts.Domain.Enums;

namespace ERP.Discounts.Application.Repositories;

public sealed record PromotionSegmentRate(
    string SegmentCode, 
    CalculationMethod CalculationMethod, 
    decimal Value);
