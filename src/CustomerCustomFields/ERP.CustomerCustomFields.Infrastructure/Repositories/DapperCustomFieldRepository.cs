using System.Data;
using System.Text.Json;
using Dapper;
using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Domain.Models;
using ERP.CustomerCustomFields.Infrastructure.Data;

namespace ERP.CustomerCustomFields.Infrastructure.Repositories;

public sealed class DapperCustomFieldRepository(string connectionString) : ICustomFieldRepository
{
    private readonly SqlConnectionFactory connections = new(connectionString);

    private static CommandDefinition Command(string name, object? args, CancellationToken token) => new(name, args, commandType: CommandType.StoredProcedure, cancellationToken: token);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<FieldDefinition> CreateAsync(CreateFieldCommand command, int actorId, CancellationToken token)
    {
        using var c = connections.Create();
        await c.OpenAsync(token);
        using var results = await c.QueryMultipleAsync(Command("dbo.CustomField_Create", new { command.FieldKey, command.Label, FieldType = (byte)command.FieldType, ActorUserId = actorId, OptionsJson = JsonSerializer.Serialize(command.Options, Json) }, token));
        var field = await results.ReadSingleAsync<FieldRow>();
        var options = (await results.ReadAsync<FieldOption>()).ToArray();
        return CustomFieldMapper.Field(field, options);
    }

    public async Task<IReadOnlyList<FieldDefinition>> ListAsync(bool includeInactive, CancellationToken token)
    {
        using var c = connections.Create();
        await c.OpenAsync(token);
        using var results = await c.QueryMultipleAsync(Command("dbo.CustomField_List", new { IncludeInactive = includeInactive }, token));
        var fields = (await results.ReadAsync<FieldRow>()).ToArray();
        var options = (await results.ReadAsync<FieldOption>()).ToArray();
        return fields.Select(f => CustomFieldMapper.Field(f, options)).ToArray();
    }

    public async Task<FieldOption> AddOptionAsync(int fieldId, CreateOptionCommand command, CancellationToken token)
    {
        using var c = connections.Create();
        await c.OpenAsync(token);
        return await c.QuerySingleAsync<FieldOption>(Command("dbo.CustomField_AddOption", new { FieldId = fieldId, command.Label, command.SortOrder }, token));
    }

    private async Task Execute(string procedure, object args, CancellationToken token)
    {
        using var c = connections.Create();
        await c.OpenAsync(token);
        await c.ExecuteAsync(Command(procedure, args, token));
    }

    public Task SetFieldActiveAsync(int fieldId, bool active, CancellationToken token) => Execute("dbo.CustomField_SetActive", new { FieldId = fieldId, IsActive = active }, token);

    public Task SetOptionActiveAsync(int fieldId, int optionId, bool active, CancellationToken token) => Execute("dbo.CustomField_SetOptionActive", new { FieldId = fieldId, OptionId = optionId, IsActive = active }, token);

    public async Task<IReadOnlyList<CustomerFieldState>> ListForCustomerAsync(Guid customerId, CancellationToken token)
    {
        using var c = connections.Create();
        await c.OpenAsync(token);
        var args = new DynamicParameters();
        args.Add("CustomerId", customerId, DbType.Guid);
        using var results = await c.QueryMultipleAsync(Command("dbo.CustomerCustomField_ListForCustomer", args, token));
        var rows = (await results.ReadAsync<ProfileRow>()).ToArray();
        var options = (await results.ReadAsync<FieldOption>()).ToArray();
        return rows.Select(r => new CustomerFieldState(CustomFieldMapper.Field(new() { FieldId = r.FieldId, FieldKey = r.FieldKey, Label = r.Label, FieldType = r.FieldType, IsActive = r.IsActive }, options), CustomFieldMapper.Value(r))).ToArray();
    }

    public async Task<CustomerFieldWriteResult> SetValueAsync(SetFieldValueCommand command, CancellationToken token)
    {
        using var c = connections.Create();
        await c.OpenAsync(token);
        var args = new DynamicParameters();
        args.Add("CustomerId", command.CustomerId, DbType.Guid);
        args.Add("FieldId", command.FieldId);
        args.Add("TextValue", command.TextValue, DbType.String, size: -1);
        args.Add("SelectedOptionIdsJson", command.SelectedOptionIds is null ? null : JsonSerializer.Serialize(command.SelectedOptionIds), DbType.String, size: -1);
        args.Add("ExpectedVersion", command.ExpectedVersion, DbType.Int64);
        args.Add("ActorUserId", command.ActorUserId);
        var row = await c.QuerySingleAsync<ValueRow>(Command("dbo.CustomerCustomField_SetValue", args, token));
        return new(CustomFieldMapper.Value(row), row.Changed);
    }

    public async Task<IReadOnlyList<FieldTransition>> GetHistoryAsync(Guid customerId, int fieldId, CancellationToken token)
    {
        using var c = connections.Create();
        await c.OpenAsync(token);
        var rows = await c.QueryAsync<HistoryRow>(Command("dbo.CustomerCustomField_GetHistory", new { CustomerId = customerId, FieldId = fieldId }, token));
        return rows.Select(r => new FieldTransition(r.ValueVersion, r.OldTextValue, r.NewTextValue, CustomFieldMapper.Ids(r.OldSelectedOptionIds), CustomFieldMapper.Ids(r.NewSelectedOptionIds), CustomFieldMapper.Utc(r.ChangedAtUtc), r.ChangedByUserId)).ToArray();
    }
}
