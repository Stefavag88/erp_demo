using Dapper;
using Microsoft.Data.SqlClient;
using ERP.CustomerCustomFields.Infrastructure.Identifiers;
using ERP.CustomerCustomFields.Infrastructure.Repositories;
using ERP.CustomerCustomFields.IntegrationTests.Sql;
namespace ERP.CustomerCustomFields.IntegrationTests.Repositories;
public class CustomerIdRoundTripTests
{
 [SqlServerFact] public async Task Application_generated_customer_GUIDs_round_trip()
 {
  await using var db=await SqlServerFixture.CreateAsync(4);var repository=new DapperCustomerRepository(db.ConnectionString);var generator=new CustomerIdGenerator();var expected=new List<Guid>();
  for(var i=0;i<20;i++){var id=generator.NewId();expected.Add(id);await repository.CreateAsync(id,"Generated "+i,default);}
  using var connection=new SqlConnection(db.ConnectionString);var actual=(await connection.QueryAsync<Guid>("SELECT CustomerId FROM dbo.Customer ORDER BY CustomerId")).ToArray();Assert.Equal(expected.Order(),actual.Order());
  using var cancellation=new CancellationTokenSource();cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repository.ListAsync(cancellation.Token));
 }
}
