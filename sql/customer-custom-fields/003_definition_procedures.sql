USE [$(DatabaseName)];

GO
-- Application error codes: 51000 validation, 51001 missing resource, 51002 conflict.
-- Inputs stay NVARCHAR(MAX) until validated so oversized values are rejected,
-- rather than silently truncated by procedure parameter binding.
CREATE FUNCTION dbo.CustomField_IsBlank
(@Value NVARCHAR (MAX))
RETURNS BIT
AS
BEGIN
    -- Unicode White_Space characters, matching the application's whitespace checks.
    DECLARE @Whitespace AS NVARCHAR (25) = NCHAR(9) + NCHAR(10) + NCHAR(11) + NCHAR(12) + NCHAR(13) + NCHAR(32) + NCHAR(133) + NCHAR(160) + NCHAR(5760) + NCHAR(8192) + NCHAR(8193) + NCHAR(8194) + NCHAR(8195) + NCHAR(8196) + NCHAR(8197) + NCHAR(8198) + NCHAR(8199) + NCHAR(8200) + NCHAR(8201) + NCHAR(8202) + NCHAR(8232) + NCHAR(8233) + NCHAR(8239) + NCHAR(8287) + NCHAR(12288);
    RETURN CASE WHEN @Value IS NULL
                     OR DATALENGTH(TRIM(@Whitespace FROM @Value)) = 0 THEN 1 ELSE 0 END;
END

GO
CREATE PROCEDURE dbo.CustomField_Create
@FieldKey NVARCHAR (MAX), @Label NVARCHAR (MAX), @FieldType TINYINT, @ActorUserId INT, @OptionsJson NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF dbo.CustomField_IsBlank(@FieldKey) = 1
       OR DATALENGTH(@FieldKey) > 200
       OR dbo.CustomField_IsBlank(@Label) = 1
       OR DATALENGTH(@Label) > 400
       OR @FieldType IS NULL
       OR @FieldType NOT IN (1, 2)
        THROW 51000, 'Invalid field key, label or type.', 1;
    IF @OptionsJson IS NULL
       OR ISJSON(@OptionsJson, ARRAY) <> 1
        THROW 51000, 'Options must be an array.', 1;
    IF @FieldType = 1
       AND EXISTS (SELECT 1
                   FROM OPENJSON (@OptionsJson))
        THROW 51000, 'Text fields cannot have options.', 1;
    IF EXISTS (SELECT 1
               FROM OPENJSON (@OptionsJson)
               WHERE type <> 5)
        THROW 51000, 'Each option must be an object.', 1;
    IF EXISTS (SELECT 1
               FROM OPENJSON (@OptionsJson) AS j
               WHERE dbo.CustomField_IsBlank(JSON_VALUE(j.value, '$.label')) = 1
                     OR DATALENGTH(JSON_VALUE(j.value, '$.label')) > 400
                     OR TRY_CONVERT (INT, JSON_VALUE(j.value, '$.sortOrder')) IS NULL)
        THROW 51000, 'Invalid option label or sort order.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1
                       FROM dbo.AppUser
                       WHERE UserId = @ActorUserId)
            THROW 51001, 'Actor not found.', 1;
        IF EXISTS (SELECT 1
                   FROM dbo.CustomFieldDefinition WITH (UPDLOCK, HOLDLOCK)
                   WHERE FieldKey = @FieldKey)
            THROW 51002, 'Field key already exists.', 1;
        INSERT dbo.CustomFieldDefinition (FieldKey, Label, FieldType, CreatedByUserId)
        VALUES                          (@FieldKey, @Label, @FieldType, @ActorUserId);
        DECLARE @FieldId AS INT = CONVERT (INT, SCOPE_IDENTITY());
        INSERT dbo.CustomFieldOption (FieldId, Label, SortOrder)
        SELECT @FieldId,
               JSON_VALUE(value, '$.label'),
               CONVERT (INT, JSON_VALUE(value, '$.sortOrder'))
        FROM OPENJSON (@OptionsJson);
        COMMIT TRANSACTION;
        SELECT FieldId,
               FieldKey,
               Label,
               FieldType,
               IsActive
        FROM dbo.CustomFieldDefinition
        WHERE FieldId = @FieldId;
        SELECT OptionId,
               FieldId,
               Label,
               SortOrder,
               IsActive
        FROM dbo.CustomFieldOption
        WHERE FieldId = @FieldId
        ORDER BY SortOrder, OptionId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK;
        THROW;
    END CATCH
END

GO
CREATE PROCEDURE dbo.CustomField_List
@IncludeInactive BIT=0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT FieldId,
           FieldKey,
           Label,
           FieldType,
           IsActive
    FROM dbo.CustomFieldDefinition
    WHERE @IncludeInactive = 1
          OR IsActive = 1
    ORDER BY FieldId;
    SELECT o.OptionId,
           o.FieldId,
           o.Label,
           o.SortOrder,
           o.IsActive
    FROM dbo.CustomFieldOption AS o
         INNER JOIN
         dbo.CustomFieldDefinition AS f
         ON f.FieldId = o.FieldId
    WHERE @IncludeInactive = 1
          OR f.IsActive = 1
    -- The repository groups by FieldId; within-field ordering is the API contract.
    ORDER BY o.FieldId, o.SortOrder, o.OptionId;
END

GO
CREATE PROCEDURE dbo.CustomField_AddOption
@FieldId INT, @Label NVARCHAR (MAX), @SortOrder INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF dbo.CustomField_IsBlank(@Label) = 1
       OR DATALENGTH(@Label) > 400
       OR @SortOrder IS NULL
        THROW 51000, 'Invalid option label or sort order.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Type AS TINYINT, @Active AS BIT;
        SELECT @Type = FieldType,
               @Active = IsActive
        FROM dbo.CustomFieldDefinition WITH (UPDLOCK, HOLDLOCK)
        WHERE FieldId = @FieldId;
        IF @Type IS NULL
            THROW 51001, 'Field not found.', 1;
        IF @Type <> 2
           OR @Active <> 1
            THROW 51000, 'Only active multi-select fields accept options.', 1;
        INSERT dbo.CustomFieldOption (FieldId, Label, SortOrder)
        VALUES                      (@FieldId, @Label, @SortOrder);
        DECLARE @Id AS INT = CONVERT (INT, SCOPE_IDENTITY());
        COMMIT TRANSACTION;
        SELECT OptionId,
               FieldId,
               Label,
               SortOrder,
               IsActive
        FROM dbo.CustomFieldOption
        WHERE OptionId = @Id;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK;
        THROW;
    END CATCH
END

GO
CREATE PROCEDURE dbo.CustomField_SetActive
@FieldId INT, @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsActive IS NULL
        THROW 51000, 'Active state is required.', 1;
    UPDATE dbo.CustomFieldDefinition
    SET IsActive = @IsActive
    WHERE FieldId = @FieldId;
    IF @@ROWCOUNT = 0
        THROW 51001, 'Field not found.', 1;
END

GO
CREATE PROCEDURE dbo.CustomField_SetOptionActive
@FieldId INT, @OptionId INT, @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @IsActive IS NULL
        THROW 51000, 'Active state is required.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Found AS INT;
        SELECT @Found = FieldId
        FROM dbo.CustomFieldDefinition WITH (UPDLOCK, HOLDLOCK)
        WHERE FieldId = @FieldId;
        IF @Found IS NULL
            THROW 51001, 'Field not found.', 1;
        UPDATE dbo.CustomFieldOption
        SET IsActive = @IsActive
        WHERE FieldId = @FieldId
              AND OptionId = @OptionId;
        IF @@ROWCOUNT = 0
            THROW 51001, 'Option not found.', 1;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK;
        THROW;
    END CATCH
END
