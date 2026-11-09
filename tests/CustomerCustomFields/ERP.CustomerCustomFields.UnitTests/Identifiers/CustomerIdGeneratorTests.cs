using ERP.CustomerCustomFields.Infrastructure.Identifiers;
namespace ERP.CustomerCustomFields.UnitTests.Identifiers;
public class CustomerIdGeneratorTests
{
 [Fact] public void Customer_generation_produces_nonempty_distinct_identifiers()
 {var generator=new CustomerIdGenerator();var ids=Enumerable.Range(0,1000).Select(_=>generator.NewId()).ToArray();Assert.DoesNotContain(Guid.Empty,ids);Assert.Equal(1000,ids.Distinct().Count());}
}
