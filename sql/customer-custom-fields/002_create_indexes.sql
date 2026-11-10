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

-- Audit reads seek one customer/field and order by version. Keep this index narrow:
-- history grows on every meaningful change, so duplicating text/JSON and audit
-- payloads would increase storage and write maintenance for occasional reads.
-- Accept base-history lookups; retain SQL Server's period index for AS OF queries.
CREATE INDEX IX_History_CustomerFieldVersion
    ON history.CustomerFieldValueHistory(CustomerId, FieldId, ValueVersion);
