using ERP.Discounts.Application.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.IntegrationTests.Sql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace ERP.Discounts.IntegrationTests.Api;

[Trait("Category","Api")]
public class PricingEndpointTests
{
    private sealed class Factory(bool fail=false,string? realConnection=null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production").UseSetting("ConnectionStrings:Discounts",realConnection ?? "Server=unused;Integrated Security=true");
            if(realConnection is null) builder.ConfigureServices(services=>
            {
                services.RemoveAll<IPriceListRepository>();services.RemoveAll<ICustomerSegmentProvider>();services.RemoveAll<IPromotionRepository>();services.RemoveAll<ICouponRepository>();
                var repositories=new DemoRepositories(fail);
                services.AddSingleton<IPriceListRepository>(repositories);services.AddSingleton<ICustomerSegmentProvider>(repositories);services.AddSingleton<IPromotionRepository>(repositories);services.AddSingleton<ICouponRepository>(repositories);
            });
        }
    }
    private sealed class DemoRepositories(bool fail) : IPriceListRepository,ICustomerSegmentProvider,IPromotionRepository,ICouponRepository
    {
        public Task<IReadOnlyList<PriceListDefinition>> GetEligibleAsync(string customerId,DateTimeOffset asOfUtc,CancellationToken cancellationToken)
        {
            if(fail)throw new IOException("connection password=do-not-expose");
            return Task.FromResult<IReadOnlyList<PriceListDefinition>>(customerId=="unknown"?[]:[new(Guid.Parse("00000011-0000-0000-0000-000000000001"),"Gold Price List",CalculationMethod.Percentage,CalculationBase.CurrentAmount,5,0)]);
        }
        public Task<string?> GetSegmentAsync(string customerId,CancellationToken cancellationToken)=>Task.FromResult<string?>(customerId=="unknown"?null:"GOLD");
        public Task<IReadOnlyList<PromotionDefinition>> GetActiveDefinitionsAsync(DateTimeOffset asOfUtc,CancellationToken cancellationToken)=>Task.FromResult<IReadOnlyList<PromotionDefinition>>([new(Guid.Parse("00000021-0000-0000-0000-000000000001"),"Segment Promotion",200,0,true,null,null,[new("GOLD",CalculationMethod.Percentage,10)])]);
        public Task<IReadOnlyList<CouponDefinition>> GetEligibleAsync(string customerId,IReadOnlyList<string> codes,DateTimeOffset asOfUtc,CancellationToken cancellationToken)=>Task.FromResult<IReadOnlyList<CouponDefinition>>(codes.Contains("WELCOME10")?[new(Guid.Parse("00000031-0000-0000-0000-000000000001"),"WELCOME10","WELCOME10",CalculationMethod.FixedAmount,CalculationBase.CurrentAmount,10,0)]:[]);
    }
    private const string Request = """{"orderId":"ORD-123","customerId":"customer-001","originalAmount":340.00,"currency":"EUR","couponCodes":["WELCOME10"]}""";
    internal static async Task AssertDemoAsync(HttpClient client)
    {
        using var response=await client.PostAsync("/api/pricing/calculate",new StringContent(Request,Encoding.UTF8,"application/json"));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var json=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ORD-123",json.GetProperty("orderId").GetString());Assert.Equal("EUR",json.GetProperty("currency").GetString());
        Assert.Equal(340m,json.GetProperty("originalAmount").GetDecimal());Assert.Equal(280.70m,json.GetProperty("finalAmount").GetDecimal());Assert.Equal(59.30m,json.GetProperty("totalDiscount").GetDecimal());
        var rows=json.GetProperty("discounts");Assert.Equal(3,rows.GetArrayLength());
        Assert.Equal("PriceList",rows[0].GetProperty("type").GetString());Assert.Equal("Percentage",rows[0].GetProperty("calculationMethod").GetString());
        Assert.Equal(1,rows[0].GetProperty("sequence").GetInt32());Assert.Equal(340m,rows[0].GetProperty("calculationBaseAmount").GetDecimal());
        Assert.Equal(JsonValueKind.Null,rows[2].GetProperty("calculationBaseAmount").ValueKind);
        Assert.Equal("CurrentAmount",rows[2].GetProperty("calculationBase").GetString());
        foreach(var row in rows.EnumerateArray())foreach(var field in new[]{"sequence","discountId","name","type","calculationMethod","calculationBase","value","calculationBaseAmount","priceBefore","discountAmount","priceAfter"})Assert.True(row.TryGetProperty(field,out _),field);
    }
    [Fact] public async Task Returns_full_demo_breakdown(){await using var factory=new Factory();using var client=factory.CreateClient();await AssertDemoAsync(client);}
    [Fact]
    public async Task Empty_eligibility_is_200()
    {
        await using var factory=new Factory();using var client=factory.CreateClient();
        using var response=await client.PostAsync("/api/pricing/calculate",new StringContent(Request.Replace("customer-001","unknown").Replace("WELCOME10","UNKNOWN"),Encoding.UTF8,"application/json"));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);var json=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(340m,json.GetProperty("finalAmount").GetDecimal());Assert.Equal(0,json.GetProperty("discounts").GetArrayLength());
    }
    [Theory]
    [InlineData("""{"orderId":"x","customerId":"c","originalAmount":1.001,"currency":"EUR","couponCodes":[]}""")]
    [InlineData("""{"orderId":"","customerId":"c","originalAmount":10,"currency":"EUR","couponCodes":[]}""")]
    [InlineData("""{"orderId":"x","customerId":"c","originalAmount":10,"currency":"EUR","couponCodes":null}""")]
    [InlineData("""{"orderId":"x","customerId":"c","currency":"EUR","couponCodes":[]}""")]
    [InlineData("null")] [InlineData("{broken")]
    public async Task Invalid_input_is_400(string request)
    {
        await using var factory=new Factory();using var client=factory.CreateClient();using var response=await client.PostAsync("/api/pricing/calculate",new StringContent(request,Encoding.UTF8,"application/json"));Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
    }
    [Fact]
    public async Task Validation_errors_identify_fields()
    {
        await using var factory=new Factory();using var client=factory.CreateClient();using var response=await client.PostAsync("/api/pricing/calculate",new StringContent(Request.Replace("EUR","USD"),Encoding.UTF8,"application/json"));
        var json=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.True(json.GetProperty("errors").TryGetProperty("currency",out _));
    }
    [Theory]
    [InlineData("1.00000000000000000000000000001")]
    [InlineData("-0.00000000000000000000000000001")]
    [InlineData("99999999999999999.990000000001")]
    [InlineData("1e-3")]
    [InlineData("-1e-29")]
    [InlineData("1e17")]
    [InlineData("1e2147483648")]
    public async Task Rejects_exact_numeric_tokens_that_decimal_binding_would_round(string amount)
    {
        await using var factory=new Factory();using var client=factory.CreateClient();
        using var response=await client.PostAsync("/api/pricing/calculate",new StringContent(Request.Replace("340.00",amount),Encoding.UTF8,"application/json"));
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        var json=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("errors").TryGetProperty("originalAmount",out _));
    }
    [Theory]
    [InlineData("340.000","340")]
    [InlineData("3.4e2","340")]
    [InlineData("1e-2","0.01")]
    [InlineData("0e99999999999999999999","0")]
    [InlineData("-0.000","0")]
    [InlineData("9.999999999999999999e16","99999999999999999.99")]
    public async Task Accepts_exact_cents_with_trailing_zeros_and_exponents(string amount,string expectedText)
    {
        var expected=decimal.Parse(expectedText,System.Globalization.CultureInfo.InvariantCulture);
        await using var factory=new Factory();using var client=factory.CreateClient();
        var body=Request.Replace("340.00",amount).Replace("customer-001","unknown").Replace("WELCOME10","UNKNOWN");
        using var response=await client.PostAsync("/api/pricing/calculate",new StringContent(body,Encoding.UTF8,"application/json"));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var json=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(expected,json.GetProperty("originalAmount").GetDecimal());Assert.Equal(expected,json.GetProperty("finalAmount").GetDecimal());
    }
    [Fact]
    public async Task Database_failure_is_500_and_hides_exception_details()
    {
        await using var factory=new Factory(fail:true);using var client=factory.CreateClient();using var response=await client.PostAsync("/api/pricing/calculate",new StringContent(Request,Encoding.UTF8,"application/json"));Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);Assert.DoesNotContain("do-not-expose",await response.Content.ReadAsStringAsync());Assert.Contains("application/problem+json",response.Content.Headers.ContentType!.ToString());
    }
    [SqlServerFact,Trait("Category","SqlServer")]
    public async Task HTTP_demo_uses_seeded_SQL_Server()
    {
        await using var db=await SqlServerFixture.CreateAsync();await using var factory=new Factory(realConnection:db.ConnectionString);using var client=factory.CreateClient();await AssertDemoAsync(client);
    }
}
