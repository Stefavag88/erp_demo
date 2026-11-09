using Dapper;
using Microsoft.Data.SqlClient;
using ERP.CustomerCustomFields.Infrastructure.Identifiers;
using ERP.CustomerCustomFields.Infrastructure.Repositories;
using ERP.CustomerCustomFields.IntegrationTests.Sql;
namespace ERP.CustomerCustomFields.IntegrationTests.Repositories;
public class CustomerIdSqlOrderingTests
{
 private sealed class Clock:TimeProvider {public DateTimeOffset Now=new(2026,11,9,12,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
 [SqlServerFact] public async Task Generated_customer_GUIDs_round_trip_and_SQL_orders_controlled_timestamps()
 {
  await using var db=await SqlServerFixture.CreateAsync(4);var repository=new DapperCustomerRepository(db.ConnectionString);var clock=new Clock();var generator=new SqlServerCustomerIdGenerator(clock);var expected=new List<Guid>();
  for(var i=0;i<20;i++){var id=generator.NewId();expected.Add(id);await repository.CreateAsync(id,"Generated "+i,default);clock.Now=clock.Now.AddMilliseconds(1);}
  using var connection=new SqlConnection(db.ConnectionString);var actual=(await connection.QueryAsync<Guid>("SELECT CustomerId FROM dbo.Customer ORDER BY CustomerId")).ToArray();Assert.Equal(expected,actual);
  using var cancellation=new CancellationTokenSource();cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repository.ListAsync(cancellation.Token));
 }
}
