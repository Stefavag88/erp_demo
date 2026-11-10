using ERP.CustomerCustomFields.IntegrationTests.Sql;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.CustomerCustomFields.IntegrationTests.Api;

public class CustomFieldCatalogCacheTests
{
    [SqlServerFact]
    public async Task Catalog_is_shared_across_requests_and_creation_invalidates_both_variants()
    {
        await using var db = await SqlServerFixture.CreateAsync(6);
        await using var factory = new CustomFieldEndpointTests.Factory(db.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/custom-fields"))!);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/custom-fields?includeInactive=true"))!);

        // Bypass this API to distinguish a cache hit from another database read.
        await db.SqlAsync("EXEC dbo.CustomField_Create N'external',N'External',1,1,N'[]';");
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/custom-fields"))!);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/custom-fields?includeInactive=true"))!);

        using var created = await client.PostAsJsonAsync("/api/custom-fields", new { fieldKey = "created", label = "Created", fieldType = "TEXT" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement[]>("/api/custom-fields"))!.Length);
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement[]>("/api/custom-fields?includeInactive=true"))!.Length);
    }
}
