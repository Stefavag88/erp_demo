using ERP.CustomerCustomFields.Application.Contracts;
namespace ERP.CustomerCustomFields.Infrastructure.Identifiers;
public sealed class SqlServerCustomerIdGenerator(TimeProvider clock) : ICustomerIdGenerator
{
 public Guid NewId() => RT.Comb.Provider.Sql.Create(clock.GetUtcNow().UtcDateTime);
}

