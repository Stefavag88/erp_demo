using ERP.Discounts.Domain.Enums;
namespace ERP.Discounts.Application;
public static class DiscountCategoryPolicy
{
    public static int GetPriority(DiscountType type) => type switch
    {
        DiscountType.PriceList => 100,
        DiscountType.Promotion => 200,
        DiscountType.Coupon => 300,
        _ => throw new InvalidOperationException($"Unknown provider category: {type}.")
    };
}
