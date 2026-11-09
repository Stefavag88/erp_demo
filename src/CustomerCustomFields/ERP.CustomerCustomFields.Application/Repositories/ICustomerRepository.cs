using ERP.CustomerCustomFields.Domain.Models;
namespace ERP.CustomerCustomFields.Application.Repositories;
public interface ICustomerRepository
{
 Task<IReadOnlyList<Customer>> ListAsync(CancellationToken token);
 Task<Customer> CreateAsync(Guid customerId,string name,CancellationToken token);
}
