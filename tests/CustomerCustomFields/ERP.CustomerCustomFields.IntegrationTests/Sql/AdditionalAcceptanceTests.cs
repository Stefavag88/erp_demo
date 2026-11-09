using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
namespace ERP.CustomerCustomFields.IntegrationTests.Sql;
public class AdditionalAcceptanceTests
{
 [SqlServerFact] public async Task Temporal_as_of_reconstructs_a_previous_committed_value()
 {
  await using var db=await ValueProcedureTests.Database();using var c=new SqlConnection(db.ConnectionString);
  await db.SqlAsync(ValueProcedureTests.Set(1,"N'before'","NULL","NULL"));
  var point=await c.ExecuteScalarAsync<DateTime>("SELECT SYSUTCDATETIME();");
  await c.ExecuteAsync("WAITFOR DELAY '00:00:00.020';");
  await db.SqlAsync(ValueProcedureTests.Set(1,"N'after'","NULL","1"));
  var parameters=new DynamicParameters();parameters.Add("Point",point,DbType.DateTime2);
  var previous=await c.QuerySingleAsync<string>("SELECT TextValue FROM dbo.CustomerFieldValue FOR SYSTEM_TIME AS OF @Point WHERE FieldId=1;",parameters);Assert.Equal("before",previous);
 }
 [SqlServerFact] public async Task Concurrent_existing_updates_allow_one_write_and_one_conflict()
 {
  await using var db=await ValueProcedureTests.Database();await db.SqlAsync(ValueProcedureTests.Set(1,"N'initial'","NULL","NULL"));
  var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  async Task<int> Write(string text)
  {
   using var c=new SqlConnection(db.ConnectionString);await c.OpenAsync();await gate.Task;
   try {await c.ExecuteAsync("dbo.CustomerCustomField_SetValue",new{CustomerId=Guid.Parse(ValueProcedureTests.CustomerA),FieldId=1,TextValue=text,SelectedOptionIdsJson=(string?)null,ExpectedVersion=1L,ActorUserId=1},commandType:CommandType.StoredProcedure);return 0;}
   catch(SqlException ex){return ex.Number;}
  }
  var first=Write("first");var second=Write("second");gate.SetResult();Assert.Equal(new[]{0,51002},(await Task.WhenAll(first,second)).Order().ToArray());
  await db.SqlAsync("IF (SELECT COUNT(*) FROM history.CustomerFieldValueHistory)<>1 THROW 50000,'Lost update or duplicate history.',1;");
 }
 [SqlServerFact] public async Task Option_deactivation_racing_assignment_serializes_to_a_valid_outcome()
 {
  await using var db=await ValueProcedureTests.Database();var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  async Task<int> Assign()
  {
   using var c=new SqlConnection(db.ConnectionString);await c.OpenAsync();await gate.Task;
   try {await c.ExecuteAsync("dbo.CustomerCustomField_SetValue",new{CustomerId=Guid.Parse(ValueProcedureTests.CustomerA),FieldId=2,TextValue=(string?)null,SelectedOptionIdsJson="[1]",ExpectedVersion=(long?)null,ActorUserId=1},commandType:CommandType.StoredProcedure);return 0;}
   catch(SqlException ex){return ex.Number;}
  }
  async Task Deactivate(){using var c=new SqlConnection(db.ConnectionString);await c.OpenAsync();await gate.Task;await c.ExecuteAsync("dbo.CustomField_SetOptionActive",new{FieldId=2,OptionId=1,IsActive=false},commandType:CommandType.StoredProcedure);}
  var assignment=Assign();var deactivation=Deactivate();gate.SetResult();await deactivation;Assert.Contains(await assignment,new[]{0,51000});
 }
 [SqlServerFact] public async Task Foreign_oversized_and_inactive_field_values_have_validation_errors()
 {
  await using var db=await ValueProcedureTests.Database();
  await db.SqlAsync("EXEC dbo.CustomField_Create N'other',N'Other',2,1,N'[{\"label\":\"Foreign\",\"sortOrder\":1}]';");
  foreach(var sql in new[]{ValueProcedureTests.Set(2,"NULL","N'[4]'","NULL"),ValueProcedureTests.Set(1,"N'"+new string('x',2001)+"'","NULL","NULL"),ValueProcedureTests.Set(2,"NULL","N'["+new string(' ',4000)+"]'","NULL")})
  {var ex=await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync(sql));Assert.Equal(51000,Assert.IsType<SqlException>(ex.InnerException).Number);}
  await db.SqlAsync("EXEC dbo.CustomField_SetActive 1,0;");var inactive=await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync(ValueProcedureTests.Set(1,"N'value'","NULL","NULL")));Assert.Equal(51000,Assert.IsType<SqlException>(inactive.InnerException).Number);
 }
}
