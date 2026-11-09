USE [$(DatabaseName)];

GO
CREATE INDEX IX_Option_Field
    ON dbo.CustomFieldOption(FieldId, SortOrder, OptionId);

CREATE INDEX IX_Value_Field
    ON dbo.CustomerFieldValue(FieldId, CustomerId);

CREATE INDEX IX_History_CustomerFieldVersion
    ON history.CustomerFieldValueHistory(CustomerId, FieldId, ValueVersion);
