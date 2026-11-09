namespace ERP.Discounts.IntegrationTests.Sql;

public class SqlServerVersionTests
{
    [Theory]
    [InlineData(15, false)]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void Fixture_requires_engine_major_16(int major, bool accepted)
    {
        if (accepted) SqlServerFixture.RequireVersion(major);
        else Assert.Throws<InvalidOperationException>(() => SqlServerFixture.RequireVersion(major));
    }

    [SqlServerFact]
    public async Task Database_has_compatibility_160()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await db.SqlAsync("IF (SELECT compatibility_level FROM sys.databases WHERE name=DB_NAME())<>160 THROW 50000,'Expected compatibility 160.',1;");
    }
}
