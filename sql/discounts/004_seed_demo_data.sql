USE [$(DatabaseName)];
GO
-- CalculationMethod: 1=Percentage, 2=FixedAmount; CalculationBase: 1=CurrentAmount, 2=OriginalAmount.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
-- Stable IDs and key-specific upserts make this repeatable without deleting user data.
MERGE dbo.PriceLists WITH (HOLDLOCK) AS target
USING (VALUES
    (N'00000001-0000-0000-0000-000000000001',N'Gold Price List',1,NULL,NULL),
    (N'00000002-0000-0000-0000-000000000001',N'Multi Price List',1,NULL,NULL)
) AS source(Id,Name,IsActive,StartsAtUtc,EndsAtUtc)
ON target.Id=source.Id
WHEN MATCHED THEN UPDATE SET Name=source.Name,IsActive=source.IsActive,StartsAtUtc=source.StartsAtUtc,EndsAtUtc=source.EndsAtUtc
WHEN NOT MATCHED THEN INSERT(Id,Name,IsActive,StartsAtUtc,EndsAtUtc) VALUES(source.Id,source.Name,source.IsActive,source.StartsAtUtc,source.EndsAtUtc);
MERGE dbo.PriceListDiscounts WITH (HOLDLOCK) AS target
USING (VALUES
    (N'00000011-0000-0000-0000-000000000001',N'00000001-0000-0000-0000-000000000001',N'Gold Price List',1,1,5,0,1),
    (N'00000012-0000-0000-0000-000000000001',N'00000002-0000-0000-0000-000000000001',N'First Multi',2,1,5,0,1),
    (N'00000013-0000-0000-0000-000000000001',N'00000002-0000-0000-0000-000000000001',N'Second Multi',1,1,5,0,1)
) AS source(Id,PriceListId,Name,CalculationMethod,CalculationBase,Value,PriorityWithinType,IsActive)
ON target.Id=source.Id
WHEN MATCHED THEN UPDATE SET PriceListId=source.PriceListId,Name=source.Name,CalculationMethod=source.CalculationMethod,CalculationBase=source.CalculationBase,Value=source.Value,PriorityWithinType=source.PriorityWithinType,IsActive=source.IsActive
WHEN NOT MATCHED THEN INSERT(Id,PriceListId,Name,CalculationMethod,CalculationBase,Value,PriorityWithinType,IsActive) VALUES(source.Id,source.PriceListId,source.Name,source.CalculationMethod,source.CalculationBase,source.Value,source.PriorityWithinType,source.IsActive);
MERGE dbo.CustomerPriceLists WITH (HOLDLOCK) AS target
USING (VALUES
    (N'10000000-0000-4000-8000-019a6a000001',N'00000001-0000-0000-0000-000000000001'),
    (N'10000000-0000-4000-8000-019a6a000002',N'00000001-0000-0000-0000-000000000001'),
    (N'10000000-0000-4000-8000-019a6a000003',N'00000002-0000-0000-0000-000000000001')
) AS source(CustomerId,PriceListId)
ON target.CustomerId=source.CustomerId AND target.PriceListId=source.PriceListId
WHEN NOT MATCHED THEN INSERT(CustomerId,PriceListId) VALUES(source.CustomerId,source.PriceListId);
MERGE dbo.Promotions WITH (HOLDLOCK) AS target
USING (VALUES
    (N'00000021-0000-0000-0000-000000000001',N'Segment Promotion',200,0,1,NULL,NULL),
    (N'00000022-0000-0000-0000-000000000001',N'VIP Fixed Promotion',0,0,1,NULL,NULL)
) AS source(Id,Name,MinimumCurrentAmount,PriorityWithinType,IsActive,StartsAtUtc,EndsAtUtc)
ON target.Id=source.Id
WHEN MATCHED THEN UPDATE SET Name=source.Name,MinimumCurrentAmount=source.MinimumCurrentAmount,PriorityWithinType=source.PriorityWithinType,IsActive=source.IsActive,StartsAtUtc=source.StartsAtUtc,EndsAtUtc=source.EndsAtUtc
WHEN NOT MATCHED THEN INSERT(Id,Name,MinimumCurrentAmount,PriorityWithinType,IsActive,StartsAtUtc,EndsAtUtc) VALUES(source.Id,source.Name,source.MinimumCurrentAmount,source.PriorityWithinType,source.IsActive,source.StartsAtUtc,source.EndsAtUtc);
MERGE dbo.PromotionSegmentRates WITH (HOLDLOCK) AS target
USING (VALUES
    (N'00000021-0000-0000-0000-000000000001',N'GOLD',1,1,10),
    (N'00000021-0000-0000-0000-000000000001',N'SILVER',1,1,5),
    (N'00000022-0000-0000-0000-000000000001',N'VIP',2,1,15)
) AS source(PromotionId,SegmentCode,CalculationMethod,CalculationBase,Value)
ON target.PromotionId=source.PromotionId AND target.SegmentCode=source.SegmentCode
WHEN MATCHED THEN UPDATE SET CalculationMethod=source.CalculationMethod,CalculationBase=source.CalculationBase,Value=source.Value
WHEN NOT MATCHED THEN INSERT(PromotionId,SegmentCode,CalculationMethod,CalculationBase,Value) VALUES(source.PromotionId,source.SegmentCode,source.CalculationMethod,source.CalculationBase,source.Value);
MERGE dbo.Coupons WITH (HOLDLOCK) AS target
USING (VALUES
    (N'00000031-0000-0000-0000-000000000001',N'WELCOME10',N'WELCOME10',2,1,10,0,1,NULL,NULL),
    (N'00000032-0000-0000-0000-000000000001',N'PRIVATE5',N'Restricted five',2,1,5,0,1,NULL,NULL),
    (N'00000033-0000-0000-0000-000000000001',N'ORIGINAL10',N'Original ten percent',1,2,10,1,1,NULL,NULL)
) AS source(Id,Code,Name,CalculationMethod,CalculationBase,Value,PriorityWithinType,IsActive,StartsAtUtc,EndsAtUtc)
ON target.Id=source.Id
WHEN MATCHED THEN UPDATE SET Code=source.Code,Name=source.Name,CalculationMethod=source.CalculationMethod,CalculationBase=source.CalculationBase,Value=source.Value,PriorityWithinType=source.PriorityWithinType,IsActive=source.IsActive,StartsAtUtc=source.StartsAtUtc,EndsAtUtc=source.EndsAtUtc
WHEN NOT MATCHED THEN INSERT(Id,Code,Name,CalculationMethod,CalculationBase,Value,PriorityWithinType,IsActive,StartsAtUtc,EndsAtUtc) VALUES(source.Id,source.Code,source.Name,source.CalculationMethod,source.CalculationBase,source.Value,source.PriorityWithinType,source.IsActive,source.StartsAtUtc,source.EndsAtUtc);
MERGE dbo.CouponCustomerEligibility WITH (HOLDLOCK) AS target
USING (VALUES
    (N'00000032-0000-0000-0000-000000000001',N'10000000-0000-4000-8000-019a6a000003')
) AS source(CouponId,CustomerId)
ON target.CouponId=source.CouponId AND target.CustomerId=source.CustomerId
WHEN NOT MATCHED THEN INSERT(CouponId,CustomerId) VALUES(source.CouponId,source.CustomerId);
COMMIT TRANSACTION;
GO
