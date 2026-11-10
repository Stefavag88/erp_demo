USE [$(DatabaseName)];

GO
CREATE PROCEDURE dbo.Customer_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CustomerId,
           Name
    FROM dbo.Customer
    ORDER BY Name, CustomerId;
END

GO
CREATE PROCEDURE dbo.Customer_Create
@CustomerId UNIQUEIDENTIFIER, @Name NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    IF @CustomerId IS NULL
       OR @CustomerId = '00000000-0000-0000-0000-000000000000'
       OR dbo.CustomField_IsBlank(@Name) = 1
       OR DATALENGTH(@Name) > 400
        THROW 51000, 'A valid customer ID and name are required.', 1;
    INSERT dbo.Customer (CustomerId, Name)
    OUTPUT inserted.CustomerId, inserted.Name
    VALUES             (@CustomerId, @Name);
END
