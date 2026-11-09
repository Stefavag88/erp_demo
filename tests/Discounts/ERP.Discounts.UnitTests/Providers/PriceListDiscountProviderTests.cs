using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Application.Providers;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.UnitTests.Engine;
namespace ERP.Discounts.UnitTests.Providers;
public class PriceListDiscountProviderTests
{
    private sealed class Repository(IReadOnlyList<PriceListDefinition> rows, PricingContext context, CancellationToken token, bool fail=false) : IPriceListRepository
    {
        public Task<IReadOnlyList<PriceListDefinition>> GetEligibleAsync(Guid customerId,DateTimeOffset asOfUtc,CancellationToken cancellationToken)
        { Assert.Equal(context.Request.CustomerId,customerId);Assert.Equal(context.AsOfUtc,asOfUtc);Assert.Equal(token,cancellationToken);if(fail) throw new IOException();return Task.FromResult(rows); }
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public async Task Maps_zero_or_many_assigned_definitions(int count)
    {
        var context=new PricingContext(PricingRequestValidationTests.Valid(),340,[],DateTimeOffset.UnixEpoch);
        using var cts=new CancellationTokenSource();
        var rows=Enumerable.Range(0,count).Select(i=>new PriceListDefinition(Guid.NewGuid(),"List "+i,CalculationMethod.Percentage,CalculationBase.OriginalAmount,5,i)).ToArray();
        var result=await new PriceListDiscountProvider(new Repository(rows,context,cts.Token)).GetEligibleAsync(context,cts.Token);
        Assert.Equal(count,result.Count);
        for(var i=0;i<count;i++){Assert.Equal(rows[i].Id.ToString("D"),result[i].Id);Assert.Equal(DiscountType.PriceList,result[i].Type);Assert.Equal(5m,result[i].Value);Assert.Equal(i,result[i].PriorityWithinType);Assert.Equal(CalculationBase.OriginalAmount,result[i].CalculationBase);}
    }
    [Fact]
    public async Task Persistence_failure_is_not_empty_eligibility()
    {
        var context=new PricingContext(PricingRequestValidationTests.Valid(),340,[],DateTimeOffset.UnixEpoch);
        await Assert.ThrowsAsync<IOException>(()=>new PriceListDiscountProvider(new Repository([],context,default,true)).GetEligibleAsync(context,default));
    }
}
