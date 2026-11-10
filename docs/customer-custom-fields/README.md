# Customer custom-fields demonstration

See [combined setup](../../SUBMISSION.md) for SQL Server 2022 provisioning and API startup on port 5081. [The ER diagram](design.md) describes relationships and tradeoffs. Native SQL and Docker SQL are supported; environment variables configure connections. Never commit a real password.

## Walkthrough

1. Open the page in Development with a fresh seeded database. Customer A and B initially have no fields.
2. Create External reference (TEXT), then Customer segments (MULTI_SELECT) with VIP, Retail and Wholesale options. Both profiles now show both fields, initially unset.
3. Set A's reference to ABC-123 and B's to XYZ-999. Values remain isolated.
4. On A, select VIP/Retail and Save; change to VIP/Wholesale and Save. History shows unset -> VIP/Retail -> VIP/Wholesale, consecutive versions, database UTC operation times and actor 1.
5. Save the unchanged selection: changed=false and no new version/history. An equivalent reverse-order request in the HTTP sample is also a no-op.
6. Clear a prior value and save: the old value remains in history. Clearing a never-set field creates no row.
7. Send an old expectedVersion from the HTTP sample: 409. Refresh before retrying. A stale version conflicts even for an otherwise unchanged value.
8. Add/deactivate options or deactivate a field. Retained inactive selections remain readable; newly selecting an inactive option is rejected. History remains accessible for inactive definitions via the Inactive fields section.
9. Create Customer C: it receives all active definitions with unset values and an application-generated regular GUID.

## HTTP contract

All routes require authentication. Development alone supplies the seeded demo identity; body/header actor IDs cannot impersonate another user. Real ERP authentication is a deliberate integration seam.

| Method | Route | Body/result |
|---|---|---|
| GET | /api/custom-fields?includeInactive=true | definitions with options; omit query for active only |
| POST | /api/custom-fields | fieldKey,label,fieldType (TEXT or MULTI_SELECT),options with label/sortOrder;201 |
| POST | /api/custom-fields/{fieldId}/options | label,sortOrder;201 |
| PATCH | /api/custom-fields/{fieldId}/active | isActive:boolean;200 |
| PATCH | /api/custom-fields/{fieldId}/options/{optionId}/active | isActive:boolean;200 |
| GET | /api/customers | customerId GUID and name |
| POST | /api/customers | name;201 with generated GUID |
| GET | /api/customers/{customerId}/custom-fields | array of {definition,value}; nullable valueVersion means never set |
| PUT | /api/customers/{customerId}/custom-fields/{fieldId} | textValue,selectedOptionIds,expectedVersion; {value,changed} |
| GET | /api/customers/{customerId}/custom-fields/{fieldId}/history | versions with explicit old/new text and selection arrays,actor,time |

Customer route IDs are GUIDs; field/option IDs are integers. expectedVersion must be present, using null only when no row is expected. Numbers must be real JSON numbers, not strings. TEXT accepts a nullable string of at most 2000 UTF-16 code units, preserving nonempty whitespace. MULTI_SELECT accepts a numeric ID array, no text, with canonical JSON storage limited to 4000 characters. Empty string/NULL clears TEXT; [] clears MULTI_SELECT. Type mismatches, duplicates, noninteger/foreign/inactive options and oversized inputs return 400.

Create returns201, read/update/no-op 200, invalid input 400, missing resources 404, duplicate field key/version conflicts 409 and unauthenticated requests 401. Errors use Problem Details with stable validation_error/not_found/conflict codes; raw infrastructure errors are not exposed. SQL returns ordered version snapshots from the temporal history table and a captured current row. The application service constructs consecutive transitions in C#. Historical values exposed by the API are represented with oldTextValue,newTextValue,oldSelectedOptionIds,newSelectedOptionIds,valueVersion,changedAtUtc,changedByUserId. IDs remain JSON arrays rather than escaped JSON strings. Initial old values are null/unset.

Run fast tests in tests/CustomerCustomFields/ERP.CustomerCustomFields.UnitTests and SQL/HTTP tests in ERP.CustomerCustomFields.IntegrationTests. Set ERP_CUSTOM_FIELDS_TEST_CONNECTION to opt into isolated real SQL Server 2022 tests. The current customer source uses no external ID-generation package and no write-retry layer.


Optional UI regression checks use the installed Node.js runtime and no npm packages:

```powershell
node --test tests/CustomerCustomFields/UI/profile.test.cjs
```

The application itself has no Node.js runtime dependency. Profile/history responses are scoped to the current customer, and text values use a multiline editor to preserve valid line breaks.
