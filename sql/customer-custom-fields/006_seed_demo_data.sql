USE [$(DatabaseName)];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.AppUser WHERE UserId=1)
BEGIN
 SET IDENTITY_INSERT dbo.AppUser ON;
 INSERT dbo.AppUser(UserId,DisplayName) VALUES(1,N'Demo user');
 SET IDENTITY_INSERT dbo.AppUser OFF;
END;
-- Fixed seed IDs make repeated demonstrations reproducible. New customers use C# Guid.NewGuid().
IF NOT EXISTS(SELECT 1 FROM dbo.Customer WHERE CustomerId='10000000-0000-4000-8000-019a6a000001')
 INSERT dbo.Customer(CustomerId,Name) VALUES('10000000-0000-4000-8000-019a6a000001',N'Customer A');
IF NOT EXISTS(SELECT 1 FROM dbo.Customer WHERE CustomerId='10000000-0000-4000-8000-019a6a000002')
 INSERT dbo.Customer(CustomerId,Name) VALUES('10000000-0000-4000-8000-019a6a000002',N'Customer B');
COMMIT;
GO
