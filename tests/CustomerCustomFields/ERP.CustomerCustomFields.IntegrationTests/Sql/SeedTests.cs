namespace ERP.CustomerCustomFields.IntegrationTests.Sql;

public class SeedTests
{
    [SqlServerFact]
    public async Task Seeding_is_repeatable_without_creating_customer_values_or_fields()
    {
        await using var db = await SqlServerFixture.CreateAsync(6);
        await db.ApplyAsync(6);
        await db.SqlAsync("IF (SELECT COUNT(*) FROM dbo.Customer)<>2 OR (SELECT COUNT(*) FROM dbo.AppUser)<>1 OR EXISTS(SELECT 1 FROM dbo.CustomFieldDefinition) OR EXISTS(SELECT 1 FROM dbo.CustomerFieldValue) THROW 50000,'Seed is not repeatable or prepopulated values.',1;");
    }
}
