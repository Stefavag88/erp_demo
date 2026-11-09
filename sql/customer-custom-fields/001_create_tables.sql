USE [$(DatabaseName)];

GO
CREATE SCHEMA history;

GO
CREATE TABLE dbo.Customer (
    CustomerId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Customer PRIMARY KEY CLUSTERED,
    Name NVARCHAR (200) NOT NULL,
    CONSTRAINT CK_Customer_Id CHECK (CustomerId <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT CK_Customer_Name CHECK (LEN(Name) > 0)
);

CREATE TABLE dbo.AppUser (
    UserId INT IDENTITY PRIMARY KEY,
    DisplayName NVARCHAR (200) NOT NULL
);

-- FieldType: 1=TEXT, 2=MULTI_SELECT.
CREATE TABLE dbo.CustomFieldDefinition (
    FieldId INT IDENTITY PRIMARY KEY,
    FieldKey NVARCHAR (100) NOT NULL UNIQUE,
    Label NVARCHAR (200) NOT NULL,
    FieldType TINYINT NOT NULL CHECK (FieldType IN (1, 2)),
    IsActive BIT DEFAULT 1 NOT NULL,
    CreatedByUserId INT NOT NULL FOREIGN KEY REFERENCES dbo.AppUser (UserId),
    CreatedAtUtc DATETIME2 (7) DEFAULT SYSUTCDATETIME() NOT NULL,
    CHECK (LEN(FieldKey) > 0
           AND LEN(Label) > 0)
);

CREATE TABLE dbo.CustomFieldOption (
    OptionId INT IDENTITY PRIMARY KEY,
    FieldId INT NOT NULL FOREIGN KEY REFERENCES dbo.CustomFieldDefinition (FieldId),
    Label NVARCHAR (200) NOT NULL CHECK (LEN(Label) > 0),
    SortOrder INT NOT NULL,
    IsActive BIT DEFAULT 1 NOT NULL
);

CREATE TABLE dbo.CustomerFieldValue (
    CustomerId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Customer (CustomerId),
    FieldId INT NOT NULL FOREIGN KEY REFERENCES dbo.CustomFieldDefinition (FieldId),
    TextValue NVARCHAR (2000) NULL,
    SelectedOptionIds NVARCHAR (4000) NULL,
    ValueVersion BIGINT NOT NULL CHECK (ValueVersion >= 1),
    ChangedByUserId INT NOT NULL FOREIGN KEY REFERENCES dbo.AppUser (UserId),
    ChangedAtUtc DATETIME2 (7) NOT NULL,
    ValidFrom DATETIME2 (7) GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo DATETIME2 (7) GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    CONSTRAINT PK_CustomerFieldValue PRIMARY KEY (CustomerId, FieldId),
    CONSTRAINT CK_CustomerFieldValue_Json CHECK (SelectedOptionIds IS NULL
                                                 OR ISJSON(SelectedOptionIds, ARRAY) = 1),
    CONSTRAINT CK_CustomerFieldValue_Shape CHECK (TextValue IS NULL
                                                  OR SelectedOptionIds IS NULL),
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo)
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE=history.CustomerFieldValueHistory));
