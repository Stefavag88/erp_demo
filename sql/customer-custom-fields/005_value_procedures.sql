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
    ORDER BY o.FieldId, o.SortOrder, o.OptionId;
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
    -- Validate and canonicalize the input before taking metadata/value locks.
    -- Field type, option ownership and active-state checks still run in the transaction.
    IF @SelectedOptionIdsJson IS NULL
        BEGIN
            IF DATALENGTH(@TextValue) > 4000
                THROW 51000, 'Text fields accept at most 2000 characters and no selection array.', 1;
            IF DATALENGTH(@TextValue) = 0
                SET @TextValue = NULL;
        END
    ELSE
        BEGIN
            IF @TextValue IS NOT NULL
               OR ISJSON(@SelectedOptionIdsJson, ARRAY) <> 1
               OR DATALENGTH(@SelectedOptionIdsJson) > 8000
                THROW 51000, 'Multi-select requires an array of numeric option IDs and no text.', 1;
            -- Retain token types and raw text: JSON strings, fractions and exponents
            -- must not become valid IDs through an implicit numeric conversion.
            DECLARE @Selection TABLE (
                JsonType INT,
                RawValue NVARCHAR (MAX),
                Id INT);
            INSERT @Selection (JsonType, RawValue, Id)
            SELECT type,
                   value,
                   TRY_CONVERT (INT, value)
            FROM OPENJSON (@SelectedOptionIdsJson);
            IF EXISTS (SELECT 1
                       FROM @Selection
                       WHERE JsonType <> 2
                             OR RawValue COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%'
                             OR Id IS NULL
                             OR Id <= 0)
                THROW 51000, 'Option IDs must be positive INT tokens.', 1;
            IF EXISTS (SELECT Id
                       FROM @Selection
                       GROUP BY Id
                       HAVING COUNT(*) > 1)
                THROW 51000, 'Duplicate option IDs are not allowed.', 1;
            INSERT @Ids (Id)
            SELECT Id
            FROM @Selection;
            SELECT @SelectedOptionIdsJson = N'[' + COALESCE (STRING_AGG(CONVERT (NVARCHAR (MAX), Id), N',') WITHIN GROUP (ORDER BY Id), N'') + N']'
            FROM @Ids;
            IF DATALENGTH(@SelectedOptionIdsJson) > 8000
                THROW 51000, 'Selection exceeds 4000 characters.', 1;
        END
    BEGIN TRY
        BEGIN TRANSACTION;
        -- Consistent lock order: definition -> customer -> options -> value key. HOLDLOCK
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
                    THROW 51000, 'Text fields accept at most 2000 characters and no selection array.', 1;
            END
        ELSE
            BEGIN
                IF @SelectedOptionIdsJson IS NULL
                    THROW 51000, 'Multi-select requires an array of numeric option IDs and no text.', 1;
                IF EXISTS (SELECT 1
                           FROM @Ids AS i
                                LEFT OUTER JOIN
                                dbo.CustomFieldOption AS o WITH (HOLDLOCK)
                                ON o.OptionId = i.Id
                                   AND o.FieldId = @FieldId
                           WHERE o.OptionId IS NULL)
                    THROW 51000, 'An option does not belong to this field.', 1;
            END
        SELECT @Version = ValueVersion,
               @OldText = TextValue,
               @OldIds = SelectedOptionIds
        FROM dbo.CustomerFieldValue WITH (UPDLOCK, HOLDLOCK)
        WHERE CustomerId = @CustomerId
              AND FieldId = @FieldId;
        -- Check the caller's version before no-op detection: stale submissions must
        -- conflict even when their proposed value happens to match the current row.
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
            -- Compare exact text bytes and lengths: ordinary SQL string equality
            -- may ignore case or trailing spaces under the database collation.
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
        INSERT @Result (CustomerId, FieldId, TextValue, SelectedOptionIds, ValueVersion, ChangedByUserId, ChangedAtUtc, Changed)
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
            INSERT @Result (CustomerId, FieldId, TextValue, SelectedOptionIds, ValueVersion, ChangedByUserId, ChangedAtUtc, Changed)
            VALUES (@CustomerId, @FieldId, NULL, CASE WHEN @Type = 2 THEN N'[]' END, NULL, NULL, NULL, 0);
        COMMIT TRANSACTION;
        -- Keep the Dapper result contract explicit if internal table variables grow.
        SELECT CustomerId,
               FieldId,
               TextValue,
               SelectedOptionIds,
               ValueVersion,
               ChangedByUserId,
               ChangedAtUtc,
               Changed
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
    -- Read the history table directly: FOR SYSTEM_TIME omits zero-duration versions.
    -- The application derives consecutive transitions from these ordered snapshots.
    -- Capture the current row once so a concurrent update cannot leave a gap or duplicate.
    DECLARE @CurrentVersion TABLE (
        ValueVersion BIGINT NOT NULL,
        TextValue NVARCHAR (2000) NULL,
        SelectedOptionIds NVARCHAR (4000) NULL,
        ChangedAtUtc DATETIME2 (7) NOT NULL,
        ChangedByUserId INT NOT NULL
    );
    INSERT @CurrentVersion (ValueVersion, TextValue, SelectedOptionIds, ChangedAtUtc, ChangedByUserId)
    SELECT ValueVersion,
           TextValue,
           SelectedOptionIds,
           ChangedAtUtc,
           ChangedByUserId
    FROM dbo.CustomerFieldValue
    WHERE CustomerId = @CustomerId
          AND FieldId = @FieldId;

    SELECT h.ValueVersion,
           h.TextValue,
           h.SelectedOptionIds,
           h.ChangedAtUtc,
           h.ChangedByUserId
    FROM history.CustomerFieldValueHistory AS h
         CROSS JOIN @CurrentVersion AS c
    WHERE h.CustomerId = @CustomerId
          AND h.FieldId = @FieldId
          AND h.ValueVersion < c.ValueVersion
    UNION ALL
    SELECT ValueVersion,
           TextValue,
           SelectedOptionIds,
           ChangedAtUtc,
           ChangedByUserId
    FROM @CurrentVersion
    ORDER BY ValueVersion;
END
