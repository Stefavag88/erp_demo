using ERP.CustomerCustomFields.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using System.Data;
using ERP.CustomerCustomFields.IntegrationTests.Sql;
using ERP.CustomerCustomFields.Infrastructure.Repositories;

namespace ERP.CustomerCustomFields.IntegrationTests.Repositories;

public class RepositoryTests
{
    [SqlServerFact]
    public async Task Shared_factory_opens_fresh_connections_with_driver_retries_disabled()
    {
        await using var db = await ValueProcedureTests.Database();
        var factory = new SqlConnectionFactory(db.ConnectionString);
        await using var first = await factory.OpenAsync(default);
        await using var second = await factory.OpenAsync(default);

        Assert.NotSame(first, second);
        Assert.Equal(ConnectionState.Open, first.State);
        Assert.Equal(ConnectionState.Open, second.State);
        Assert.Equal(0, new SqlConnectionStringBuilder(first.ConnectionString).ConnectRetryCount);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.OpenAsync(cancellation.Token));

        await using var afterCancellation = await factory.OpenAsync(default);
        Assert.Equal(ConnectionState.Open, afterCancellation.State);
    }

    [SqlServerFact]
    public async Task Repository_collections_cannot_be_replaced_through_list_casts()
    {
        await using var db = await ValueProcedureTests.Database();
        var connections = new SqlConnectionFactory(db.ConnectionString);
        var fields = new DapperCustomFieldRepository(connections);
        var customers = new DapperCustomerRepository(connections);
        var customerId = Guid.Parse(ValueProcedureTests.CustomerA);

        AssertReadOnly(await fields.ListAsync(false, default));
        AssertReadOnly(await fields.ListForCustomerAsync(customerId, default));
        AssertReadOnly(await customers.ListAsync(default));

        await fields.SetValueAsync(new(customerId, 1, "ABC", null, null, 1), default);
        AssertReadOnly(await fields.GetHistoryAsync(customerId, 1, default));
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> items)
    {
        Assert.NotEmpty(items);
        var list = Assert.IsAssignableFrom<IList<T>>(items);
        Assert.Throws<NotSupportedException>(() => list[0] = items[0]);
    }

    [SqlServerFact]
    public async Task Profiles_include_all_definitions_and_values_stay_customer_specific()
    {
        await using var db = await ValueProcedureTests.Database();
        var repository = new DapperCustomFieldRepository(new SqlConnectionFactory(db.ConnectionString));
        var first = await repository.ListForCustomerAsync(Guid.Parse(ValueProcedureTests.CustomerA), default);
        Assert.Equal(2, first.Count);
        Assert.All(first, s => Assert.Null(s.Value.ValueVersion));
        var write = await repository.SetValueAsync(new(Guid.Parse(ValueProcedureTests.CustomerA), 1, "ABC", null, null, 1), default);
        Assert.True(write.Changed);
        Assert.Equal(1, write.Value.ValueVersion);
        var second = await repository.ListForCustomerAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000002"), default);
        Assert.All(second, s => Assert.Null(s.Value.ValueVersion));
        var history = await repository.GetHistoryAsync(Guid.Parse(ValueProcedureTests.CustomerA), 1, default);
        Assert.Equal("ABC", Assert.Single(history).TextValue);
    }
}
