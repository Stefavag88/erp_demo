using ERP.CustomerCustomFields.IntegrationTests.Sql;
using ERP.CustomerCustomFields.Infrastructure.Repositories;
namespace ERP.CustomerCustomFields.IntegrationTests.Repositories;
public class RepositoryTests
{
 [SqlServerFact] public async Task Profiles_include_all_definitions_and_values_stay_customer_specific()
 {
  await using var db=await ValueProcedureTests.Database();var repository=new DapperCustomFieldRepository(db.ConnectionString);
  var first=await repository.ListForCustomerAsync(Guid.Parse(ValueProcedureTests.CustomerA),default);Assert.Equal(2,first.Count);Assert.All(first,s=>Assert.Null(s.Value.ValueVersion));
  var write=await repository.SetValueAsync(new(Guid.Parse(ValueProcedureTests.CustomerA),1,"ABC",null,null,1),default);Assert.True(write.Changed);Assert.Equal(1,write.Value.ValueVersion);
  var second=await repository.ListForCustomerAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000002"),default);Assert.All(second,s=>Assert.Null(s.Value.ValueVersion));
  var history=await repository.GetHistoryAsync(Guid.Parse(ValueProcedureTests.CustomerA),1,default);Assert.Equal("ABC",Assert.Single(history).NewTextValue);
 }
}
