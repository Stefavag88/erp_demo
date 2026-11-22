USE [$(DatabaseName)];
GO
CREATE OR ALTER PROCEDURE dbo.usp_PriceList_GetEligible
    @CustomerId uniqueidentifier, @AsOfUtc datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    -- @AsOfUtc is sampled once per pricing request. Validity is [start,end), with
    -- NULL bounds unbounded. Ordering and monetary calculation stay in C#.
    SELECT d.Id,d.Name,d.CalculationMethod,d.CalculationBase,d.Value,d.PriorityWithinType
    FROM dbo.CustomerPriceLists a
    JOIN dbo.PriceLists p ON p.Id=a.PriceListId
    JOIN dbo.PriceListDiscounts d ON d.PriceListId=p.Id
    WHERE a.CustomerId=@CustomerId AND p.IsActive=1 AND d.IsActive=1
      AND (p.StartsAtUtc IS NULL OR p.StartsAtUtc<=@AsOfUtc)
      AND (p.EndsAtUtc IS NULL OR @AsOfUtc<p.EndsAtUtc);
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Promotion_GetDefinitions
AS
BEGIN
    SET NOCOUNT ON;
    -- Cache active promotions across their full validity windows so the provider can evaluate dates per request.
    SELECT p.Id AS PromotionId,p.Name,p.MinimumCurrentAmount,p.PriorityWithinType,p.IsActive,p.StartsAtUtc,p.EndsAtUtc,
           r.SegmentCode,r.CalculationMethod,r.CalculationBase,r.Value
    FROM dbo.Promotions p
    INNER JOIN dbo.PromotionSegmentRates r ON r.PromotionId=p.Id
    WHERE p.IsActive=1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Coupon_GetEligible
    @CustomerId uniqueidentifier, @Codes dbo.CouponCodeList READONLY, @AsOfUtc datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.Id,c.Code,c.Name,c.CalculationMethod,c.CalculationBase,c.Value,c.PriorityWithinType
    FROM dbo.Coupons c JOIN @Codes requested ON requested.Code=c.Code
    WHERE c.IsActive=1 AND (c.StartsAtUtc IS NULL OR c.StartsAtUtc<=@AsOfUtc)
      AND (c.EndsAtUtc IS NULL OR @AsOfUtc<c.EndsAtUtc)
      -- An empty allow-list means unrestricted; otherwise require this customer.
      AND (NOT EXISTS(SELECT 1 FROM dbo.CouponCustomerEligibility e WHERE e.CouponId=c.Id)
        OR EXISTS(SELECT 1 FROM dbo.CouponCustomerEligibility e WHERE e.CouponId=c.Id AND e.CustomerId=@CustomerId));
END;
GO
