USE [$(DatabaseName)];
GO
-- The TVP matches Coupons.Code's binary collation and carries normalized unique
-- codes. It is a request parameter, not a persisted coupon-redemption table.
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
-- Coupon reads start from requested codes; the PK handles returned row lookups.
CREATE UNIQUE INDEX UX_Coupons_Code ON dbo.Coupons(Code);
-- Support price-list ownership checks in the reverse direction. Customer lookups
-- already use CustomerPriceLists' (CustomerId, PriceListId) primary key.
CREATE INDEX IX_CustomerPriceLists_PriceList ON dbo.CustomerPriceLists(PriceListId,CustomerId);
-- Cover the active-discount projection for each assigned list. Id is available
-- from the clustered key and does not need another INCLUDE entry.
CREATE INDEX IX_PriceListDiscounts_List ON dbo.PriceListDiscounts(PriceListId,IsActive) INCLUDE(Name,CalculationMethod,CalculationBase,Value,PriorityWithinType);
-- PKs cover customer assignment, promotion rates and coupon allow-list lookups.
-- No standalone IsActive/window indexes: these reads first identify a list/code,
-- while promotions intentionally load a full catalog for the in-memory cache.
GO
