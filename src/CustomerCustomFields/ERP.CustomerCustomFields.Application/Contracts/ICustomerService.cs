using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Application.Contracts;

public interface ICustomerService
{
    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken token);
    Task<Customer> CreateAsync(string name, CancellationToken token);
}
