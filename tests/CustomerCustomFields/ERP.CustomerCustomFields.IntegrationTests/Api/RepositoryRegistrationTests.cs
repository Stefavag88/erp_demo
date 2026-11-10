using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Infrastructure.Data;
using ERP.CustomerCustomFields.Infrastructure.Repositories;
using ERP.CustomerCustomFields.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.CustomerCustomFields.IntegrationTests.Api;

public class RepositoryRegistrationTests
{
    [Fact]
    public async Task Cached_fields_are_singletons_and_customers_are_scoped_with_a_shared_connection_factory()
    {
        await using var factory = new CustomFieldEndpointTests.Factory("Server=unused;Integrated Security=true", "Production");
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<SqlConnectionFactory>(),
            second.ServiceProvider.GetRequiredService<SqlConnectionFactory>());

        var customers = first.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var fields = first.ServiceProvider.GetRequiredService<ICustomFieldRepository>();
        Assert.IsType<DapperCustomerRepository>(customers);
        Assert.IsType<CachedCustomFieldRepository>(fields);
        var dapperFields = first.ServiceProvider.GetRequiredService<DapperCustomFieldRepository>();
        Assert.Same(
            dapperFields,
            second.ServiceProvider.GetRequiredService<DapperCustomFieldRepository>());
        Assert.Same(customers, first.ServiceProvider.GetRequiredService<ICustomerRepository>());
        Assert.Same(fields, first.ServiceProvider.GetRequiredService<ICustomFieldRepository>());
        Assert.NotSame(customers, second.ServiceProvider.GetRequiredService<ICustomerRepository>());
        Assert.Same(fields, second.ServiceProvider.GetRequiredService<ICustomFieldRepository>());
    }
}
