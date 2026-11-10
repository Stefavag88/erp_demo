using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Application.Services;
using ERP.CustomerCustomFields.Domain.Enums;
using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.UnitTests.Services;

public class CustomFieldServiceTests
{
    private sealed class Actor : ICurrentActor
    {
        public int UserId => 42;
    }

    private sealed class Repository : ICustomFieldRepository
    {
        public int Writes { get; private set; }
        public int? ActorId { get; private set; }
        public CreateFieldCommand? Created { get; private set; }
        public SetFieldValueCommand? Assigned { get; private set; }
        public bool? IncludeInactive { get; private set; }
        public CancellationToken Token { get; private set; }
        public IReadOnlyList<FieldValueVersion> Versions { get; set; } = [];
        public Guid? HistoryCustomerId { get; private set; }
        public int? HistoryFieldId { get; private set; }
        private static FieldDefinition Definition => new(7, "tags", "Tags", CustomFieldType.MultiSelect, true, [new(9, 7, "VIP", 1, true)]);

        public Task<FieldDefinition> CreateAsync(CreateFieldCommand command, int actorId, CancellationToken token)
        {
            Writes++;
            Created = command;
            ActorId = actorId;
            Token = token;
            return Task.FromResult(Definition with { FieldType = command.FieldType });
        }

        public Task<IReadOnlyList<FieldDefinition>> ListAsync(bool includeInactive, CancellationToken token)
        {
            IncludeInactive = includeInactive;
            Token = token;
            return Task.FromResult<IReadOnlyList<FieldDefinition>>([Definition]);
        }

        public Task<FieldOption> AddOptionAsync(int fieldId, CreateOptionCommand command, CancellationToken token)
        {
            Writes++;
            return Task.FromResult(new FieldOption(9, fieldId, command.Label, command.SortOrder, true));
        }

        public Task SetFieldActiveAsync(int fieldId, bool active, CancellationToken token) => Task.CompletedTask;
        public Task SetOptionActiveAsync(int fieldId, int optionId, bool active, CancellationToken token) => Task.CompletedTask;

        public Task<IReadOnlyList<CustomerFieldState>> ListForCustomerAsync(Guid customerId, CancellationToken token)
            => Task.FromResult<IReadOnlyList<CustomerFieldState>>([new(Definition, new(customerId, 7, null, [9], 3, 42, DateTimeOffset.UnixEpoch))]);

        public Task<CustomerFieldWriteResult> SetValueAsync(SetFieldValueCommand command, CancellationToken token)
        {
            Writes++;
            Assigned = command;
            Token = token;
            return Task.FromResult(new CustomerFieldWriteResult(new(command.CustomerId, command.FieldId, command.TextValue, command.SelectedOptionIds, 1, command.ActorUserId, DateTimeOffset.UnixEpoch), true));
        }

        public Task<IReadOnlyList<FieldValueVersion>> GetHistoryAsync(Guid customerId, int fieldId, CancellationToken token)
        {
            HistoryCustomerId = customerId;
            HistoryFieldId = fieldId;
            Token = token;
            return Task.FromResult(Versions);
        }
    }

    [Fact]
    public async Task Text_history_pairs_consecutive_versions_including_clearing_and_restoring()
    {
        var time = DateTimeOffset.UtcNow;
        var repository = new Repository
        {
            Versions = [new(1, "A", null, time, 1), new(2, "a ", null, time, 2), new(3, null, null, time, 3), new(4, "B", null, time, 4)]
        };
        var customer = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var service = new CustomFieldService(repository, new Actor());

        var history = await service.GetHistoryAsync(customer, 7, cancellation.Token);

        Assert.Equal(new string?[] { null, "A", "a ", null }, history.Select(row => row.OldTextValue));
        Assert.Equal(new string?[] { "A", "a ", null, "B" }, history.Select(row => row.NewTextValue));
        Assert.Equal(new long[] { 1, 2, 3, 4 }, history.Select(row => row.ValueVersion));
        Assert.Equal(new[] { 1, 2, 3, 4 }, history.Select(row => row.ChangedByUserId));
        Assert.All(history, row => Assert.Equal(time, row.ChangedAtUtc));
        Assert.All(history, row => Assert.Null(row.OldSelectedOptionIds));
        Assert.All(history, row => Assert.Null(row.NewSelectedOptionIds));
        Assert.Equal(customer, repository.HistoryCustomerId);
        Assert.Equal(7, repository.HistoryFieldId);
        Assert.Equal(cancellation.Token, repository.Token);
        Assert.Throws<NotSupportedException>(() => ((IList<FieldTransition>)history).Clear());
    }

    [Fact]
    public async Task Selection_history_distinguishes_unset_from_empty_and_preserves_whole_sets()
    {
        var time = DateTimeOffset.UtcNow;
        var repository = new Repository
        {
            Versions = [new(1, null, [1, 3], time, 1), new(2, null, [2, 3], time, 1), new(3, null, [], time, 1)]
        };
        var service = new CustomFieldService(repository, new Actor());

        var history = await service.GetHistoryAsync(Guid.NewGuid(), 7, default);

        Assert.Null(history[0].OldSelectedOptionIds);
        Assert.Equal([1, 3], history[0].NewSelectedOptionIds);
        Assert.Equal([1, 3], history[1].OldSelectedOptionIds);
        Assert.Equal([2, 3], history[1].NewSelectedOptionIds);
        Assert.Equal([2, 3], history[2].OldSelectedOptionIds);
        Assert.Empty(history[2].NewSelectedOptionIds!);
        Assert.All(history, row => Assert.Null(row.NewTextValue));
    }

    [Fact]
    public async Task History_is_empty_when_there_are_no_stored_versions()
    {
        var service = new CustomFieldService(new Repository(), new Actor());
        Assert.Empty(await service.GetHistoryAsync(Guid.NewGuid(), 7, default));
    }

    [Theory]
    [InlineData("TEXT", CustomFieldType.Text)]
    [InlineData("MULTI_SELECT", CustomFieldType.MultiSelect)]
    public async Task Creation_resolves_type_options_and_authenticated_actor(string type, CustomFieldType expected)
    {
        var repository = new Repository();
        var service = new CustomFieldService(repository, new Actor());
        using var cancellation = new CancellationTokenSource();

        var response = await service.CreateAsync(new("tags", "Tags", type), cancellation.Token);

        Assert.Equal(type, response.FieldType);
        Assert.Equal(expected, repository.Created!.FieldType);
        Assert.Empty(repository.Created.Options);
        Assert.Equal(42, repository.ActorId);
        Assert.Equal(cancellation.Token, repository.Token);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("1")]
    [InlineData("text")]
    public async Task Invalid_field_types_are_rejected_before_persistence(string type)
    {
        var repository = new Repository();
        var service = new CustomFieldService(repository, new Actor());
        await Assert.ThrowsAsync<CustomFieldException>(() => service.CreateAsync(new("key", "Label", type), default));
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task Validation_cannot_be_bypassed_by_calling_the_service_directly()
    {
        var repository = new Repository();
        var service = new CustomFieldService(repository, new Actor());
        await Assert.ThrowsAsync<CustomFieldException>(() => service.CreateAsync(new("key", "Label", "TEXT", [new("VIP", 1)]), default));
        await Assert.ThrowsAsync<CustomFieldException>(() => service.AddOptionAsync(7, new("\t", 1), default));
        await Assert.ThrowsAsync<CustomFieldException>(() => service.SetValueAsync(Guid.Empty, 7, new() { TextValue = "Value", ExpectedVersion = null }, default));
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task Assignment_preserves_raw_text_version_and_claim_actor_for_SQL_validation()
    {
        var repository = new Repository();
        var service = new CustomFieldService(repository, new Actor());
        var customerId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();

        var result = await service.SetValueAsync(customerId, 7, new() { TextValue = "", ExpectedVersion = 3 }, cancellation.Token);

        Assert.True(result.Changed);
        Assert.Equal("", repository.Assigned!.TextValue);
        Assert.Equal(3, repository.Assigned.ExpectedVersion);
        Assert.Equal(42, repository.Assigned.ActorUserId);
        Assert.Equal(customerId, repository.Assigned.CustomerId);
        Assert.Equal(cancellation.Token, repository.Token);
    }

    [Fact]
    public async Task Lists_default_to_active_fields_and_project_named_types_with_customer_values()
    {
        var repository = new Repository();
        var service = new CustomFieldService(repository, new Actor());
        var definitions = await service.ListAsync(null, default);
        Assert.False(repository.IncludeInactive);
        Assert.Equal("MULTI_SELECT", Assert.Single(definitions).FieldType);
        Assert.Equal("VIP", Assert.Single(definitions[0].Options).Label);

        var customerId = Guid.NewGuid();
        var state = Assert.Single(await service.ListForCustomerAsync(customerId, default));
        Assert.Equal("MULTI_SELECT", state.Definition.FieldType);
        Assert.Equal(customerId, state.Value.CustomerId);
        Assert.Equal(3, state.Value.ValueVersion);
        Assert.Equal([9], state.Value.SelectedOptionIds);
    }
}
