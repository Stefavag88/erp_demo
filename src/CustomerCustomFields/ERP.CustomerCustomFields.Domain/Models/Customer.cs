namespace ERP.CustomerCustomFields.Domain.Models;

public sealed class Customer
{
    public Guid CustomerId { get; }
    public string Name { get; }

    public Customer(Guid customerId, string name)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("A nonempty customer identifier is required.", nameof(customerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 200)
            throw new ArgumentOutOfRangeException(nameof(name), "Customer names cannot exceed 200 characters.");

        CustomerId = customerId;
        Name = name;
    }
}
