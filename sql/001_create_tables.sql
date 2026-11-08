USE [$(DatabaseName)];
GO
SET XACT_ABORT ON;
GO
CREATE TABLE dbo.PriceLists (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_PriceLists PRIMARY KEY,
    Name nvarchar(200) NOT NULL,
    IsActive bit NOT NULL,
    StartsAtUtc datetime2(3) NULL,
    EndsAtUtc datetime2(3) NULL,
    CONSTRAINT CK_PriceLists_Window CHECK (StartsAtUtc IS NULL OR EndsAtUtc IS NULL OR StartsAtUtc < EndsAtUtc)
);
CREATE TABLE dbo.CustomerPriceLists (
    CustomerId nvarchar(64) NOT NULL,
    PriceListId uniqueidentifier NOT NULL CONSTRAINT FK_CustomerPriceLists_List REFERENCES dbo.PriceLists(Id),
    CONSTRAINT PK_CustomerPriceLists PRIMARY KEY (CustomerId, PriceListId)
);
CREATE TABLE dbo.PriceListDiscounts (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_PriceListDiscounts PRIMARY KEY,
    PriceListId uniqueidentifier NOT NULL CONSTRAINT FK_PriceListDiscounts_List REFERENCES dbo.PriceLists(Id),
    Name nvarchar(200) NOT NULL,
    CalculationMethod varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CalculationBase varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Value decimal(18,4) NOT NULL,
    PriorityWithinType int NOT NULL,
    IsActive bit NOT NULL,
    CONSTRAINT CK_PriceListDiscounts_Method CHECK(CalculationMethod IN ('Percentage','FixedAmount')),
    CONSTRAINT CK_PriceListDiscounts_Base CHECK(CalculationBase IN ('CurrentAmount','OriginalAmount')),
    CONSTRAINT CK_PriceListDiscounts_Value CHECK(Value >= 0 AND (CalculationMethod <> 'Percentage' OR Value <= 100)),
    CONSTRAINT CK_PriceListDiscounts_Fixed CHECK(CalculationMethod <> 'FixedAmount' OR CalculationBase = 'CurrentAmount')
);
CREATE TABLE dbo.CustomerDiscountProfiles (
    CustomerId nvarchar(64) NOT NULL CONSTRAINT PK_CustomerDiscountProfiles PRIMARY KEY,
    SegmentCode nvarchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL
);
CREATE TABLE dbo.Promotions (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Promotions PRIMARY KEY,
    Name nvarchar(200) NOT NULL,
    CalculationBase varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    MinimumCurrentAmount decimal(19,2) NOT NULL,
    PriorityWithinType int NOT NULL,
    IsActive bit NOT NULL,
    StartsAtUtc datetime2(3) NULL,
    EndsAtUtc datetime2(3) NULL,
    CONSTRAINT CK_Promotions_Base CHECK(CalculationBase IN ('CurrentAmount','OriginalAmount')),
    CONSTRAINT CK_Promotions_Minimum CHECK(MinimumCurrentAmount >= 0),
    CONSTRAINT CK_Promotions_Window CHECK(StartsAtUtc IS NULL OR EndsAtUtc IS NULL OR StartsAtUtc < EndsAtUtc)
);
CREATE TABLE dbo.PromotionSegmentRates (
    PromotionId uniqueidentifier NOT NULL CONSTRAINT FK_PromotionSegmentRates_Promotion REFERENCES dbo.Promotions(Id),
    SegmentCode nvarchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CalculationMethod varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Value decimal(18,4) NOT NULL,
    CONSTRAINT PK_PromotionSegmentRates PRIMARY KEY(PromotionId,SegmentCode),
    CONSTRAINT CK_PromotionSegmentRates_Method CHECK(CalculationMethod IN ('Percentage','FixedAmount')),
    CONSTRAINT CK_PromotionSegmentRates_Value CHECK(Value >= 0 AND (CalculationMethod <> 'Percentage' OR Value <= 100))
);
CREATE TABLE dbo.Coupons (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Coupons PRIMARY KEY,
    Code nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Name nvarchar(200) NOT NULL,
    CalculationMethod varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CalculationBase varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Value decimal(18,4) NOT NULL,
    PriorityWithinType int NOT NULL,
    IsActive bit NOT NULL,
    StartsAtUtc datetime2(3) NULL,
    EndsAtUtc datetime2(3) NULL,
    CONSTRAINT CK_Coupons_Method CHECK(CalculationMethod IN ('Percentage','FixedAmount')),
    CONSTRAINT CK_Coupons_Base CHECK(CalculationBase IN ('CurrentAmount','OriginalAmount')),
    CONSTRAINT CK_Coupons_Value CHECK(Value >= 0 AND (CalculationMethod <> 'Percentage' OR Value <= 100)),
    CONSTRAINT CK_Coupons_Fixed CHECK(CalculationMethod <> 'FixedAmount' OR CalculationBase = 'CurrentAmount'),
    CONSTRAINT CK_Coupons_Window CHECK(StartsAtUtc IS NULL OR EndsAtUtc IS NULL OR StartsAtUtc < EndsAtUtc)
);
CREATE TABLE dbo.CouponCustomerEligibility (
    CouponId uniqueidentifier NOT NULL CONSTRAINT FK_CouponCustomerEligibility_Coupon REFERENCES dbo.Coupons(Id),
    CustomerId nvarchar(64) NOT NULL,
    CONSTRAINT PK_CouponCustomerEligibility PRIMARY KEY(CouponId,CustomerId)
);
GO
-- Enforce the invariant in both mutation directions, for all affected rows.
CREATE OR ALTER TRIGGER dbo.tr_PromotionRates_ValidateFixedBase ON dbo.PromotionSegmentRates AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.Promotions p WITH (UPDLOCK,HOLDLOCK) ON p.Id=i.PromotionId WHERE i.CalculationMethod='FixedAmount' AND p.CalculationBase<>'CurrentAmount')
        THROW 50001, 'Fixed promotion rates require CurrentAmount.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.tr_Promotions_ValidateFixedBase ON dbo.Promotions AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.PromotionSegmentRates r WITH (UPDLOCK,HOLDLOCK) ON r.PromotionId=i.Id WHERE r.CalculationMethod='FixedAmount' AND i.CalculationBase<>'CurrentAmount')
        THROW 50001, 'Fixed promotion rates require CurrentAmount.', 1;
END;
GO
