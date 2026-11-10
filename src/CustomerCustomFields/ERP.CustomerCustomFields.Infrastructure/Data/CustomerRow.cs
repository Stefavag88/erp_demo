using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Infrastructure.Data;

internal sealed class CustomerRow
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = "";

    internal Customer ToCustomer() => new(CustomerId, Name);
}
