using ERP.CustomerCustomFields.IntegrationTests.Sql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Program = ERP.CustomerCustomFields.Api.Program;

namespace ERP.CustomerCustomFields.IntegrationTests.Api;

public class CustomFieldEndpointTests
{
    internal sealed class Factory(string connection, string environment = "Development") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(environment).UseSetting("ConnectionStrings:CustomerCustomFields", connection);
    }

    [SqlServerFact]
    public async Task Added_options_reject_all_blank_labels_at_HTTP_and_SQL_boundaries()
    {
        await using var db = await ValueProcedureTests.Database();
        await using var factory = new Factory(db.ConnectionString);
        using var client = factory.CreateClient();
        foreach (var blank in new[]
        {
            "\t",
            "\r\n",
            "\u00a0",
            "\u2003"
        }

        )
        {
            using var response = await client.PostAsJsonAsync("/api/custom-fields/2/options", new { label = blank, sortOrder = 1 });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync("DECLARE @label nvarchar(200)=N'" + blank + "'; EXEC dbo.CustomField_AddOption 2,@label,1;"));
            Assert.Equal(51000, Assert.IsType<Microsoft.Data.SqlClient.SqlException>(exception.InnerException).Number);
        }
    }

    [Fact]
    public async Task Outside_Development_requests_are_unauthenticated()
    {
        await using var factory = new Factory("Server=unused", "Production");
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customers")).StatusCode);
    }

    [SqlServerFact]
    public async Task Create_profile_value_history_and_concurrency_flow()
    {
        await using var db = await SqlServerFixture.CreateAsync(5);
        await DefinitionProcedureTests.Setup(db);
        await using var factory = new Factory(db.ConnectionString);
        using var client = factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/custom-fields", new { fieldKey = "ref", label = "Reference", fieldType = "TEXT", options = Array.Empty<object>(), changedByUserId = 999 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var field = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fieldId").GetInt32();
        var path = $"/api/customers/{ValueProcedureTests.CustomerA}/custom-fields/{field}";
        var missing = await client.PutAsJsonAsync(path, new { textValue = "ABC" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var write = await client.PutAsJsonAsync(path, new { textValue = "ABC", selectedOptionIds = (int[]? )null, expectedVersion = (long? )null, changedByUserId = 999 });
        Assert.Equal(HttpStatusCode.OK, write.StatusCode);
        var stale = await client.PutAsJsonAsync(path, new { textValue = "ABC", expectedVersion = (long? )null });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var history = await client.GetFromJsonAsync<JsonElement[]>(path + "/history");
        Assert.Equal("ABC", Assert.Single(history!).GetProperty("newTextValue").GetString());
        Assert.Equal(1, history![0].GetProperty("changedByUserId").GetInt32());
        var next = await client.PostAsJsonAsync("/api/customers", new { name = "New customer" });
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        var id = (await next.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("customerId").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        var profile = await client.GetFromJsonAsync<JsonElement[]>($"/api/customers/{id}/custom-fields");
        Assert.Single(profile!);
        Assert.Equal(JsonValueKind.Null, profile![0].GetProperty("value").GetProperty("valueVersion").ValueKind);
    }

    [SqlServerFact]
    public async Task Multi_select_rejects_supplied_empty_text_and_requires_expected_version()
    {
        await using var db = await ValueProcedureTests.Database();
        await using var factory = new Factory(db.ConnectionString);
        using var client = factory.CreateClient();
        var path = $"/api/customers/{ValueProcedureTests.CustomerA}/custom-fields/2";
        var json = new
        {
            textValue = "",
            selectedOptionIds = Array.Empty<int>(),
            expectedVersion = (long? )null
        };
        var response = await client.PutAsJsonAsync(path, json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        foreach (var version in new[]
        {
            "null",
            "0",
            "-1",
            "1.5",
            "\"1\""
        }

        )
        {
            var versionedJson = "{\"selectedOptionIds\":[1],\"expectedVersion\":" + version + "}";
            using var r = await client.PutAsync(path, new StringContent(versionedJson, Encoding.UTF8, "application/json"));
            Assert.Equal(version == "null" ? HttpStatusCode.OK : HttpStatusCode.BadRequest, r.StatusCode);
        }
    }
}
