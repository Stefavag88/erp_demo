using Dapper;
using Microsoft.Data.SqlClient;

namespace ERP.CustomerCustomFields.IntegrationTests.Sql;

public class ValueProcedureTests
{
    internal const string CustomerA = "10000000-0000-4000-8000-019a6a000001";

    internal static async Task<SqlServerFixture> Database()
    {
        var db = await SqlServerFixture.CreateAsync(5);
        await DefinitionProcedureTests.Setup(db);
        await db.SqlAsync("EXEC dbo.CustomField_Create N'ref',N'Reference',1,1,N'[]'; EXEC dbo.CustomField_Create N'tags',N'Tags',2,1,N'[{\"label\":\"VIP\",\"sortOrder\":1},{\"label\":\"Retail\",\"sortOrder\":2},{\"label\":\"Wholesale\",\"sortOrder\":3}]';");
        return db;
    }

    internal static string Set(int field, string text, string json, string version) => $"EXEC dbo.CustomerCustomField_SetValue '{CustomerA}',{field},{text},{json},{version},1;";

    [SqlServerFact]
    public async Task Text_history_preserves_consecutive_exact_changes_and_noops()
    {
        await using var db = await Database();
        await db.SqlAsync(Set(1, "NULL", "NULL", "NULL") + "IF EXISTS(SELECT 1 FROM dbo.CustomerFieldValue) THROW 50000,'Empty created a row.',1;");
        await db.SqlAsync(Set(1, "N'A'", "NULL", "NULL"));
        await db.SqlAsync(Set(1, "N'A'", "NULL", "1"));
        await db.SqlAsync(Set(1, "N'a '", "NULL", "1"));
        await db.SqlAsync(Set(1, "N''", "NULL", "2"));
        using var connection = new SqlConnection(db.ConnectionString);
        var history = (await connection.QueryAsync("dbo.CustomerCustomField_GetHistory", new { CustomerId = Guid.Parse(CustomerA), FieldId = 1 }, commandType: System.Data.CommandType.StoredProcedure)).ToArray();
        Assert.Equal(3, history.Length);
        Assert.Null((string? )history[0].OldTextValue);
        Assert.Equal("A", (string)history[1].OldTextValue);
        Assert.Equal("a ", (string)history[1].NewTextValue);
        Assert.Null((string? )history[2].NewTextValue);
        Assert.All(history, h =>
        {
            Assert.NotEqual(default, (DateTime)h.ChangedAtUtc);
            Assert.Equal(1, (int)h.ChangedByUserId);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync(Set(1, "NULL", "NULL", "2")));
    }

    [SqlServerFact]
    public async Task Selection_replacement_is_one_change_and_reordering_is_a_noop()
    {
        await using var db = await Database();
        await db.SqlAsync(Set(2, "NULL", "N'[3,1]'", "NULL"));
        await db.SqlAsync(Set(2, "NULL", "N'[1,3]'", "1"));
        await db.SqlAsync("IF (SELECT ValueVersion FROM dbo.CustomerFieldValue WHERE FieldId=2)<>1 OR EXISTS(SELECT 1 FROM history.CustomerFieldValueHistory) THROW 50000,'Reorder produced history.',1;");
        await db.SqlAsync(Set(2, "NULL", "N'[2,3]'", "1"));
        await db.SqlAsync("IF (SELECT COUNT(*) FROM history.CustomerFieldValueHistory)<>1 THROW 50000,'Expected one whole-set change.',1;");
        await db.SqlAsync("EXEC dbo.CustomField_SetOptionActive 2,2,0;" + Set(2, "NULL", "N'[2,3]'", "2"));
        await db.SqlAsync(Set(2, "NULL", "N'[3]'", "2"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync(Set(2, "NULL", "N'[2,3]'", "3")));
        await db.SqlAsync("EXEC dbo.CustomField_SetActive 2,0; EXEC dbo.CustomerCustomField_GetHistory '" + CustomerA + "',2;");
    }

    [SqlServerFact]
    public async Task Invalid_selection_members_types_and_storage_shapes_are_rejected()
    {
        await using var db = await Database();
        foreach (var json in new[]
        {
            "[1,1]",
            "[\"1\"]",
            "[1.0]",
            "[1e0]",
            "[null]",
            "{}",
            "[2147483648]",
            "[999]",
            "[-1]",
            "[0]"
        }

        )
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync(Set(2, "NULL", "N'" + json + "'", "NULL")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync(Set(2, "N''", "N'[]'", "NULL")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync(Set(1, "N'text'", "N'[]'", "NULL")));
    }

    [SqlServerFact]
    public async Task Concurrent_first_assignments_allow_exactly_one_writer()
    {
        await using var db = await Database();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<int> Write(string value)
        {
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            await gate.Task;
            try
            {
                await c.ExecuteAsync("dbo.CustomerCustomField_SetValue", new { CustomerId = Guid.Parse(CustomerA), FieldId = 1, TextValue = value, SelectedOptionIdsJson = (string? )null, ExpectedVersion = (long? )null, ActorUserId = 1 }, commandType: System.Data.CommandType.StoredProcedure);
                return 0;
            }
            catch (SqlException ex)
            {
                return ex.Number;
            }
        }

        var first = Write("first");
        var second = Write("second");
        gate.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Equal(new[] { 0, 51002 }, results.Order().ToArray());
    }
}
