using ERP.CustomerCustomFields.Application.Contracts;

namespace ERP.CustomerCustomFields.Infrastructure.Identifiers;

public sealed class CustomerIdGenerator : ICustomerIdGenerator
{
    public Guid NewId() => Guid.NewGuid();
}
