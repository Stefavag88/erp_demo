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
    [SqlServerFact]
    public async Task History_builds_consecutive_transitions_in_CSharp_including_zero_duration_versions()
    {
        await using var db = await ValueProcedureTests.Database();
        await db.SqlAsync("BEGIN TRANSACTION;"
            + ValueProcedureTests.Set(1, "N'A'", "NULL", "NULL")
            + ValueProcedureTests.Set(1, "N'a '", "NULL", "1")
            + ValueProcedureTests.Set(1, "N''", "NULL", "2")
            + ValueProcedureTests.Set(2, "NULL", "N'[1,3]'", "NULL")
            + ValueProcedureTests.Set(2, "NULL", "N'[2]'", "1")
            + ValueProcedureTests.Set(2, "NULL", "N'[]'", "2")
            + "COMMIT TRANSACTION;");
        await using var factory = new Factory(db.ConnectionString);
        using var client = factory.CreateClient();
        var path = $"/api/customers/{ValueProcedureTests.CustomerA}/custom-fields";

        var text = await client.GetFromJsonAsync<JsonElement[]>(path + "/1/history");
        Assert.Equal(3, text!.Length);
        Assert.Equal(JsonValueKind.Null, text[0].GetProperty("oldTextValue").ValueKind);
        Assert.Equal("A", text[1].GetProperty("oldTextValue").GetString());
        Assert.Equal("a ", text[1].GetProperty("newTextValue").GetString());
        Assert.Equal("a ", text[2].GetProperty("oldTextValue").GetString());
        Assert.Equal(JsonValueKind.Null, text[2].GetProperty("newTextValue").ValueKind);
        Assert.Equal(new long[] { 1, 2, 3 }, text.Select(row => row.GetProperty("valueVersion").GetInt64()));
        Assert.All(text, row =>
        {
            Assert.Equal(1, row.GetProperty("changedByUserId").GetInt32());
            Assert.Equal(TimeSpan.Zero, row.GetProperty("changedAtUtc").GetDateTimeOffset().Offset);
        });

        var selections = await client.GetFromJsonAsync<JsonElement[]>(path + "/2/history");
        Assert.Equal(3, selections!.Length);
        Assert.Equal(JsonValueKind.Null, selections[0].GetProperty("oldSelectedOptionIds").ValueKind);
        Assert.Equal(new[] { 1, 3 }, selections[1].GetProperty("oldSelectedOptionIds").EnumerateArray().Select(id => id.GetInt32()));
        Assert.Equal(new[] { 2 }, selections[1].GetProperty("newSelectedOptionIds").EnumerateArray().Select(id => id.GetInt32()));
        Assert.Equal(new[] { 2 }, selections[2].GetProperty("oldSelectedOptionIds").EnumerateArray().Select(id => id.GetInt32()));
        Assert.Empty(selections[2].GetProperty("newSelectedOptionIds").EnumerateArray());

        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>(
            "/api/customers/10000000-0000-4000-8000-019a6a000002/custom-fields/1/history"))!);
    }

    [SqlServerFact]
    public async Task Service_backed_metadata_routes_preserve_lifecycle_and_response_contracts()
    {
        await using var db = await SqlServerFixture.CreateAsync(6);
        await using var factory = new Factory(db.ConnectionString);
        using var client = factory.CreateClient();

        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers");
        Assert.Equal(2, customers!.Length);

        var input = new { fieldKey = "tags", label = "Tags", fieldType = "MULTI_SELECT" };
        using var created = await client.PostAsJsonAsync("/api/custom-fields", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var field = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = field.GetProperty("fieldId").GetInt32();
        Assert.Equal("MULTI_SELECT", field.GetProperty("fieldType").GetString());

        using var duplicate = await client.PostAsJsonAsync("/api/custom-fields", input);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var added = await client.PostAsJsonAsync($"/api/custom-fields/{id}/options", new { label = "VIP", sortOrder = 1 });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var optionId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("optionId").GetInt32();

        using var optionState = await client.PatchAsJsonAsync($"/api/custom-fields/{id}/options/{optionId}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, optionState.StatusCode);

        var profilePath = $"/api/customers/{ValueProcedureTests.CustomerA}/custom-fields";
        var profile = await client.GetFromJsonAsync<JsonElement[]>(profilePath);
        var definition = Assert.Single(profile!).GetProperty("definition");
        Assert.Equal("MULTI_SELECT", definition.GetProperty("fieldType").GetString());
        Assert.False(definition.GetProperty("options")[0].GetProperty("isActive").GetBoolean());

        using var inactive = await client.PatchAsJsonAsync($"/api/custom-fields/{id}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>(profilePath))!);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/custom-fields"))!);
        Assert.Single((await client.GetFromJsonAsync<JsonElement[]>("/api/custom-fields?includeInactive=true"))!);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>($"{profilePath}/{id}/history"))!);

        using var active = await client.PatchAsJsonAsync($"/api/custom-fields/{id}/active", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<JsonElement[]>(profilePath))!);

        using var missing = await client.GetAsync($"{profilePath}/999/history");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

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
