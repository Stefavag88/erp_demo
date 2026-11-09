namespace ERP.CustomerCustomFields.IntegrationTests.Sql;

public class SchemaTests
{
    [SqlServerFact]
    public async Task Full_deployment_refuses_an_existing_database()
    {
        await using var db = await SqlServerFixture.CreateAsync(0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.ApplyAsync(0));
        await db.SqlAsync("IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0) THROW 50000,'Existing database was changed.',1;");
    }

    [SqlServerFact]
    public async Task Schema_is_temporal_with_GUID_customer_and_numeric_field_type()
    {
        await using var db = await SqlServerFixture.CreateAsync(1);
        await db.SqlAsync("IF NOT EXISTS(SELECT 1 FROM sys.tables WHERE object_id=OBJECT_ID('dbo.CustomerFieldValue') AND temporal_type=2) THROW 50000,'Temporal value table missing.',1; IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Customer') AND name='CustomerId' AND system_type_id=36) THROW 50000,'Customer GUID missing.',1; IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.CustomFieldDefinition') AND name='FieldType' AND system_type_id=48) THROW 50000,'Numeric enum missing.',1;");
    }
}
