namespace ERP.Discounts.Application.Repositories;

public interface ICustomerSegmentRepository 
{ 
    Task<string?> GetSegmentAsync(string customerId, CancellationToken cancellationToken); 
}
