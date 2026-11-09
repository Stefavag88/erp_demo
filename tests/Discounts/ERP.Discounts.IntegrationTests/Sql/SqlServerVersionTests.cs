namespace ERP.Discounts.IntegrationTests.Sql;

public class SqlServerVersionTests
{
    [SqlServerFact]
    public async Task Full_deployment_refuses_an_existing_database()
    {
        await using var db = await SqlServerFixture.CreateAsync(0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.ApplyAsync(0));
        await db.SqlAsync("IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0) THROW 50000,'Existing database was changed.',1;");
    }

    [Theory]
    [InlineData(15, false)]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void Fixture_requires_engine_major_16(int major, bool accepted)
    {
        if (accepted)
            SqlServerFixture.RequireVersion(major);
        else
            Assert.Throws<InvalidOperationException>(() => SqlServerFixture.RequireVersion(major));
    }

    [SqlServerFact]
    public async Task Database_has_compatibility_160()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await db.SqlAsync("IF (SELECT compatibility_level FROM sys.databases WHERE name=DB_NAME())<>160 THROW 50000,'Expected compatibility 160.',1;");
    }
}
