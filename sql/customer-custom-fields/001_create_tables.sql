USE [$(DatabaseName)];
GO
CREATE SCHEMA history;
GO
CREATE TABLE dbo.Customer(
 CustomerId uniqueidentifier NOT NULL CONSTRAINT PK_Customer PRIMARY KEY CLUSTERED,
 Name nvarchar(200) NOT NULL,
 CONSTRAINT CK_Customer_Id CHECK(CustomerId<>'00000000-0000-0000-0000-000000000000'),
 CONSTRAINT CK_Customer_Name CHECK(LEN(Name)>0)
);
CREATE TABLE dbo.AppUser(
 UserId int IDENTITY PRIMARY KEY, DisplayName nvarchar(200) NOT NULL
);
-- FieldType: 1=TEXT, 2=MULTI_SELECT.
CREATE TABLE dbo.CustomFieldDefinition(
 FieldId int IDENTITY PRIMARY KEY,
 FieldKey nvarchar(100) NOT NULL UNIQUE,
 Label nvarchar(200) NOT NULL,
 FieldType tinyint NOT NULL CHECK(FieldType IN(1,2)),
 IsActive bit NOT NULL DEFAULT 1,
 CreatedByUserId int NOT NULL REFERENCES dbo.AppUser(UserId),
 CreatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CHECK(LEN(FieldKey)>0 AND LEN(Label)>0)
);
CREATE TABLE dbo.CustomFieldOption(
 OptionId int IDENTITY PRIMARY KEY,
 FieldId int NOT NULL REFERENCES dbo.CustomFieldDefinition(FieldId),
 Label nvarchar(200) NOT NULL CHECK(LEN(Label)>0),
 SortOrder int NOT NULL,
 IsActive bit NOT NULL DEFAULT 1
);
CREATE TABLE dbo.CustomerFieldValue(
 CustomerId uniqueidentifier NOT NULL REFERENCES dbo.Customer(CustomerId),
 FieldId int NOT NULL REFERENCES dbo.CustomFieldDefinition(FieldId),
 TextValue nvarchar(2000) NULL,
 SelectedOptionIds nvarchar(4000) NULL,
 ValueVersion bigint NOT NULL CHECK(ValueVersion>=1),
 ChangedByUserId int NOT NULL REFERENCES dbo.AppUser(UserId),
 ChangedAtUtc datetime2(7) NOT NULL,
 ValidFrom datetime2(7) GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
 ValidTo datetime2(7) GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
 PERIOD FOR SYSTEM_TIME(ValidFrom,ValidTo),
 CONSTRAINT PK_CustomerFieldValue PRIMARY KEY(CustomerId,FieldId),
 CONSTRAINT CK_CustomerFieldValue_Json CHECK(SelectedOptionIds IS NULL OR ISJSON(SelectedOptionIds,ARRAY)=1),
 CONSTRAINT CK_CustomerFieldValue_Shape CHECK(TextValue IS NULL OR SelectedOptionIds IS NULL)
) WITH(SYSTEM_VERSIONING=ON(HISTORY_TABLE=history.CustomerFieldValueHistory));
GO
