using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ERP.CustomerCustomFields.IntegrationTests.Sql;

public class DefinitionProcedureTests
{
    [SqlServerFact]
    public async Task Customer_creation_rejects_Unicode_blank_names_at_the_SQL_boundary()
    {
        await using var db = await SqlServerFixture.CreateAsync(4);
        using var connection = new SqlConnection(db.ConnectionString);
        foreach (var blank in new[] { "\t", "\u00a0", "\u2003", "\r\n" })
        {
            var exception = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(
                "dbo.Customer_Create", new { CustomerId = Guid.NewGuid(), Name = blank }, commandType: CommandType.StoredProcedure));
            Assert.Equal(51000, exception.Number);
        }

        var id = Guid.NewGuid();
        await connection.ExecuteAsync("dbo.Customer_Create", new { CustomerId = id, Name = "Customer A" }, commandType: CommandType.StoredProcedure);
        Assert.Equal("Customer A", await connection.ExecuteScalarAsync<string>("SELECT Name FROM dbo.Customer WHERE CustomerId=@id", new { id }));
    }

    internal static Task Setup(SqlServerFixture db) => db.SqlAsync("INSERT dbo.AppUser(DisplayName) VALUES(N'Demo'); INSERT dbo.Customer VALUES('10000000-0000-4000-8000-019a6a000001',N'A'),('10000000-0000-4000-8000-019a6a000002',N'B');");

    [SqlServerFact]
    public async Task Definitions_do_not_prepopulate_values_and_invalid_types_or_options_are_rejected()
    {
        await using var db = await SqlServerFixture.CreateAsync(4);
        await Setup(db);
        await db.SqlAsync("EXEC dbo.CustomField_Create N'ref',N'Reference',1,1,N'[]'; IF EXISTS(SELECT 1 FROM dbo.CustomerFieldValue) THROW 50000,'Values prepopulated.',1;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync("EXEC dbo.CustomField_Create N'bad',N'Bad',1,1,N'[{\"label\":\"No\",\"sortOrder\":1}]';"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync("EXEC dbo.CustomField_Create N'bad',N'Bad',3,1,N'[]';"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync("EXEC dbo.CustomField_Create N'ref',N'Duplicate',1,1,N'[]';"));
    }

    [SqlServerFact]
    public async Task Options_can_be_added_and_soft_deactivated_without_deletion()
    {
        await using var db = await SqlServerFixture.CreateAsync(4);
        await Setup(db);
        await db.SqlAsync("EXEC dbo.CustomField_Create N'tags',N'Tags',2,1,N'[]'; EXEC dbo.CustomField_AddOption 1,N'VIP',1; EXEC dbo.CustomField_SetOptionActive 1,1,0; IF NOT EXISTS(SELECT 1 FROM dbo.CustomFieldOption WHERE OptionId=1 AND IsActive=0) THROW 50000,'Option deleted.',1;");
    }
}
