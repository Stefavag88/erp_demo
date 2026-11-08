USE [$(DatabaseName)];
GO
CREATE TYPE dbo.CouponCodeList AS TABLE (
    Code nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY
);
GO
ALTER TABLE dbo.Coupons ADD CONSTRAINT CK_Coupons_NormalizedCode CHECK (
    DATALENGTH(Code) BETWEEN 2 AND 128
    AND DATALENGTH(Code)=DATALENGTH(LTRIM(RTRIM(Code)))
    AND REPLACE(REPLACE(Code,N'_',N''),N'-',N'') NOT LIKE N'%[^A-Z0-9]%' COLLATE Latin1_General_100_BIN2
    AND LEFT(Code,1) LIKE N'[A-Z0-9]' COLLATE Latin1_General_100_BIN2
);
CREATE UNIQUE INDEX UX_Coupons_Code ON dbo.Coupons(Code);
CREATE INDEX IX_CustomerPriceLists_PriceList ON dbo.CustomerPriceLists(PriceListId,CustomerId);
CREATE INDEX IX_PriceListDiscounts_List ON dbo.PriceListDiscounts(PriceListId,IsActive) INCLUDE(Name,CalculationMethod,CalculationBase,Value,PriorityWithinType);
-- PKs cover customer assignment, promotion rates and coupon allow-list lookups.
GO
