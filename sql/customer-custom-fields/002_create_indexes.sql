USE [$(DatabaseName)];

GO
-- Customer_List returns the complete list in this order. The GUID tie-breaker makes
-- equal display names deterministic without a separate Sort operator.
CREATE INDEX IX_Customer_Name
    ON dbo.Customer(Name, CustomerId);

-- Definition/profile reads group options by field, then display order. Cover the
-- returned metadata so those reads do not need a lookup for every option.
CREATE INDEX IX_Option_Field
    ON dbo.CustomFieldOption(FieldId, SortOrder, OptionId)
    INCLUDE (Label, IsActive);

-- Reverse access path for field ownership/FK checks. Customer-specific profile and
-- value writes already use the clustered (CustomerId, FieldId) primary key.
CREATE INDEX IX_Value_Field
    ON dbo.CustomerFieldValue(FieldId, CustomerId);

-- Audit reads seek one customer/field and return consecutive version snapshots.
-- Covering the payload avoids history-clustered-index lookups per version. This
-- duplicates text/JSON storage and adds work to temporal writes; reassess that
-- tradeoff with real retention volumes. Keep SQL Server's period index for AS OF.
CREATE INDEX IX_History_CustomerFieldVersion
    ON history.CustomerFieldValueHistory(CustomerId, FieldId, ValueVersion)
    INCLUDE (TextValue, SelectedOptionIds, ChangedAtUtc, ChangedByUserId);
