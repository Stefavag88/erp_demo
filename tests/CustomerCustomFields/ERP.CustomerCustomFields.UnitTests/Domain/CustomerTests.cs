using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.UnitTests.Domain;

public class CustomerTests
{
    [Fact]
    public void Rejects_an_empty_customer_identifier()
        => Assert.ThrowsAny<ArgumentException>(() => new Customer(Guid.Empty, "Customer"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\u2003")]
    public void Rejects_blank_names(string? name)
        => Assert.ThrowsAny<ArgumentException>(() => new Customer(Guid.NewGuid(), name!));

    [Fact]
    public void Enforces_the_name_limit_without_changing_valid_names()
    {
        var id = Guid.NewGuid();
        var name = " " + new string('x', 198) + " ";
        var customer = new Customer(id, name);
        Assert.Equal(id, customer.CustomerId);
        Assert.Equal(name, customer.Name);
        Assert.ThrowsAny<ArgumentException>(() => new Customer(id, new string('x', 201)));
    }
}
