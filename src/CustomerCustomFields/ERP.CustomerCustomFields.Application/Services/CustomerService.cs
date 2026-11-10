using ERP.CustomerCustomFields.Domain.Models;
using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;

namespace ERP.CustomerCustomFields.Application.Services;

public sealed class CustomerService(ICustomerRepository repository, ICustomerIdGenerator generator) : ICustomerService
{
    public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken token)
        => repository.ListAsync(token);

    public Task<Customer> CreateAsync(string name, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new CustomFieldException("Customer name must contain 1 to 200 characters.");

        return repository.CreateAsync(generator.NewId(), name, token);
    }
}
