USE [$(DatabaseName)];

GO
CREATE PROCEDURE dbo.CustomerCustomField_ListForCustomer
@CustomerId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1
                   FROM dbo.Customer
                   WHERE CustomerId = @CustomerId)
        THROW 51001, 'Customer not found.', 1;
    SELECT f.FieldId,
           f.FieldKey,
           f.Label,
           f.FieldType,
           f.IsActive,
           @CustomerId AS CustomerId,
           v.TextValue,
           v.SelectedOptionIds,
           v.ValueVersion,
           v.ChangedByUserId,
           v.ChangedAtUtc
    FROM dbo.CustomFieldDefinition AS f
         LEFT OUTER JOIN
         dbo.CustomerFieldValue AS v
         ON v.FieldId = f.FieldId
            AND v.CustomerId = @CustomerId
    WHERE f.IsActive = 1
    ORDER BY f.FieldId;
    SELECT o.OptionId,
           o.FieldId,
           o.Label,
           o.SortOrder,
           o.IsActive
    FROM dbo.CustomFieldOption AS o
         INNER JOIN
         dbo.CustomFieldDefinition AS f
         ON f.FieldId = o.FieldId
    WHERE f.IsActive = 1
    ORDER BY o.SortOrder, o.OptionId;
END

GO
CREATE PROCEDURE dbo.CustomerCustomField_SetValue
@CustomerId UNIQUEIDENTIFIER, @FieldId INT, @TextValue NVARCHAR (MAX), @SelectedOptionIdsJson NVARCHAR (MAX), @ExpectedVersion BIGINT, @ActorUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Type AS TINYINT, @Active AS BIT, @Version AS BIGINT, @OldText AS NVARCHAR (2000), @OldIds AS NVARCHAR (4000), @Changed AS BIT = 0;
    DECLARE @Ids TABLE (
        Id INT PRIMARY KEY);
    DECLARE @Result TABLE (
        CustomerId UNIQUEIDENTIFIER,
        FieldId INT,
        TextValue NVARCHAR (2000),
        SelectedOptionIds NVARCHAR (4000),
        ValueVersion BIGINT,
        ChangedByUserId INT,
        ChangedAtUtc DATETIME2 (7),
        Changed BIT);
    BEGIN TRY
        BEGIN TRANSACTION;
        -- Consistent lock order: definition -> options -> customer/value key. HOLDLOCK
        -- retains metadata shared locks until commit; metadata mutations lock definition first.
        SELECT @Type = FieldType,
               @Active = IsActive
        FROM dbo.CustomFieldDefinition WITH (HOLDLOCK)
        WHERE FieldId = @FieldId;
        IF @Type IS NULL
            THROW 51001, 'Field not found.', 1;
        IF @Active <> 1
            THROW 51000, 'Field is inactive.', 1;
        IF NOT EXISTS (SELECT 1
                       FROM dbo.Customer WITH (HOLDLOCK)
                       WHERE CustomerId = @CustomerId)
            THROW 51001, 'Customer not found.', 1;
        IF NOT EXISTS (SELECT 1
                       FROM dbo.AppUser
                       WHERE UserId = @ActorUserId)
            THROW 51001, 'Actor not found.', 1;
        IF @Type = 1
            BEGIN
                IF @SelectedOptionIdsJson IS NOT NULL
                   OR DATALENGTH(@TextValue) > 4000
                    THROW 51000, 'Text fields accept at most 2000 characters and no selection array.', 1;
                IF DATALENGTH(@TextValue) = 0
                    SET @TextValue = NULL;
            END
        ELSE
            BEGIN
                IF @TextValue IS NOT NULL
                   OR @SelectedOptionIdsJson IS NULL
                   OR ISJSON(@SelectedOptionIdsJson, ARRAY) <> 1
                   OR DATALENGTH(@SelectedOptionIdsJson) > 8000
                    THROW 51000, 'Multi-select requires an array of numeric option IDs and no text.', 1;
                IF EXISTS (SELECT 1
                           FROM OPENJSON (@SelectedOptionIdsJson)
                           WHERE type <> 2
                                 OR value COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%'
                                 OR TRY_CONVERT (INT, value) IS NULL
                                 OR TRY_CONVERT (INT, value) <= 0)
                    THROW 51000, 'Option IDs must be positive INT tokens.', 1;
                IF EXISTS (SELECT CONVERT (INT, value)
                           FROM OPENJSON (@SelectedOptionIdsJson)
                           GROUP BY CONVERT (INT, value)
                           HAVING COUNT(*) > 1)
                    THROW 51000, 'Duplicate option IDs are not allowed.', 1;
                INSERT @Ids
                SELECT CONVERT (INT, value)
                FROM OPENJSON (@SelectedOptionIdsJson);
                IF EXISTS (SELECT 1
                           FROM @Ids AS i
                                LEFT OUTER JOIN
                                dbo.CustomFieldOption AS o WITH (HOLDLOCK)
                                ON o.OptionId = i.Id
                                   AND o.FieldId = @FieldId
                           WHERE o.OptionId IS NULL)
                    THROW 51000, 'An option does not belong to this field.', 1;
                SELECT @SelectedOptionIdsJson = N'[' + COALESCE (STRING_AGG(CONVERT (NVARCHAR (MAX), Id), N',') WITHIN GROUP (ORDER BY Id), N'') + N']'
                FROM @Ids;
                IF DATALENGTH(@SelectedOptionIdsJson) > 8000
                    THROW 51000, 'Selection exceeds 4000 characters.', 1;
            END
        SELECT @Version = ValueVersion,
               @OldText = TextValue,
               @OldIds = SelectedOptionIds
        FROM dbo.CustomerFieldValue WITH (UPDLOCK, HOLDLOCK)
        WHERE CustomerId = @CustomerId
              AND FieldId = @FieldId;
        IF (@Version IS NULL
            AND @ExpectedVersion IS NOT NULL)
           OR (@Version IS NOT NULL
               AND (@ExpectedVersion IS NULL
                    OR @Version <> @ExpectedVersion))
            THROW 51002, 'Value version conflict. Refresh the customer profile.', 1;
        IF @Type = 2
           AND EXISTS (SELECT 1
                       FROM @Ids AS i
                            INNER JOIN
                            dbo.CustomFieldOption AS o WITH (HOLDLOCK)
                            ON o.OptionId = i.Id
                       WHERE o.IsActive = 0
                             AND NOT EXISTS (SELECT 1
                                             FROM OPENJSON (@OldIds)
                                             WHERE CONVERT (INT, value) = i.Id))
            THROW 51000, 'Inactive options cannot be newly selected.', 1;
        IF @Version IS NULL
            BEGIN
                IF @TextValue IS NOT NULL
                   OR (@Type = 2
                       AND @SelectedOptionIdsJson <> N'[]')
                    BEGIN
                        INSERT dbo.CustomerFieldValue (CustomerId, FieldId, TextValue, SelectedOptionIds, ValueVersion, ChangedByUserId, ChangedAtUtc)
                        VALUES                       (@CustomerId, @FieldId, @TextValue, @SelectedOptionIdsJson, 1, @ActorUserId, SYSUTCDATETIME());
                        SET @Changed = 1;
                    END
            END
        ELSE
            IF (@Type = 1
                AND ((@OldText IS NULL
                      AND @TextValue IS NOT NULL)
                     OR (@OldText IS NOT NULL
                         AND @TextValue IS NULL)
                     OR DATALENGTH(@OldText) <> DATALENGTH(@TextValue)
                     OR CONVERT (VARBINARY (MAX), @OldText) <> CONVERT (VARBINARY (MAX), @TextValue)))
               OR (@Type = 2
                   AND @OldIds <> @SelectedOptionIdsJson)
                BEGIN
                    UPDATE dbo.CustomerFieldValue
                    SET TextValue         = @TextValue,
                        SelectedOptionIds = @SelectedOptionIdsJson,
                        ValueVersion      = ValueVersion + 1,
                        ChangedByUserId   = @ActorUserId,
                        ChangedAtUtc      = SYSUTCDATETIME()
                    WHERE CustomerId = @CustomerId
                          AND FieldId = @FieldId;
                    SET @Changed = 1;
                END
        INSERT @Result
        SELECT @CustomerId,
               @FieldId,
               TextValue,
               SelectedOptionIds,
               ValueVersion,
               ChangedByUserId,
               ChangedAtUtc,
               @Changed
        FROM dbo.CustomerFieldValue
        WHERE CustomerId = @CustomerId
              AND FieldId = @FieldId;
        IF NOT EXISTS (SELECT 1
                       FROM @Result)
            INSERT @Result
            VALUES (@CustomerId, @FieldId, NULL, CASE WHEN @Type = 2 THEN N'[]' END, NULL, NULL, NULL, 0);
        COMMIT TRANSACTION;
        SELECT *
        FROM @Result;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK;
        THROW;
    END CATCH
END

GO
CREATE PROCEDURE dbo.CustomerCustomField_GetHistory
@CustomerId UNIQUEIDENTIFIER, @FieldId INT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1
                   FROM dbo.Customer
                   WHERE CustomerId = @CustomerId)
        THROW 51001, 'Customer not found.', 1;
    IF NOT EXISTS (SELECT 1
                   FROM dbo.CustomFieldDefinition
                   WHERE FieldId = @FieldId)
        THROW 51001, 'Field not found.', 1;
    SELECT ValueVersion,
           LAG(TextValue) OVER (ORDER BY ValueVersion) AS OldTextValue,
           TextValue AS NewTextValue,
           LAG(SelectedOptionIds) OVER (ORDER BY ValueVersion) AS OldSelectedOptionIds,
           SelectedOptionIds AS NewSelectedOptionIds,
           ChangedAtUtc,
           ChangedByUserId
    FROM dbo.CustomerFieldValue FOR SYSTEM_TIME ALL
    WHERE CustomerId = @CustomerId
          AND FieldId = @FieldId
    ORDER BY ValueVersion;
END
