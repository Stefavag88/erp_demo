using System.Collections.Concurrent;
using System.Data.SqlTypes;
using ERP.CustomerCustomFields.Infrastructure.Identifiers;
namespace ERP.CustomerCustomFields.UnitTests.Identifiers;
public class SqlServerCustomerIdGeneratorTests
{
 private sealed class Clock:TimeProvider {public DateTimeOffset Now=new(2026,11,9,12,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
 [Fact] public void Controlled_distinct_timestamps_sort_in_SQL_Server_order()
 {
  var clock=new Clock();var generator=new SqlServerCustomerIdGenerator(clock);var first=generator.NewId();clock.Now=clock.Now.AddMilliseconds(1);var second=generator.NewId();
  Assert.NotEqual(Guid.Empty,first);Assert.True(new SqlGuid(first).CompareTo(new SqlGuid(second))<0);
  Assert.Equal(clock.Now.UtcDateTime,RT.Comb.Provider.Sql.GetTimestamp(second));
 }
 [Fact] public void Concurrent_same_tick_generation_is_unique_without_order_claims()
 {
  var generator=new SqlServerCustomerIdGenerator(new Clock());var ids=new ConcurrentBag<Guid>();Parallel.For(0,10000,_=>ids.Add(generator.NewId()));Assert.DoesNotContain(Guid.Empty,ids);Assert.Equal(10000,ids.Distinct().Count());
 }
}
