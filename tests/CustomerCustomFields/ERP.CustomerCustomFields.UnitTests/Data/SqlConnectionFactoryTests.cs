using ERP.CustomerCustomFields.Infrastructure.Data;

namespace ERP.CustomerCustomFields.UnitTests.Data;

public class SqlConnectionFactoryTests
{
    [Fact]
    public void Invalid_connection_configuration_fails_when_the_factory_is_constructed()
        => Assert.Throws<ArgumentException>(() => new SqlConnectionFactory("not-a-connection-string"));
}
