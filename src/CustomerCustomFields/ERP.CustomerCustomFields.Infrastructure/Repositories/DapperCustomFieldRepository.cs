using Dapper;
using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Domain.Models;
using ERP.CustomerCustomFields.Infrastructure.Data;
using System.Data;
using System.Text.Json;

namespace ERP.CustomerCustomFields.Infrastructure.Repositories;

public sealed class DapperCustomFieldRepository(SqlConnectionFactory connections) : ICustomFieldRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<FieldDefinition> CreateAsync(CreateFieldCommand command, int actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("FieldKey", command.FieldKey, DbType.String, size: -1);
        parameters.Add("Label", command.Label, DbType.String, size: -1);
        parameters.Add("FieldType", (byte)command.FieldType, DbType.Byte);
        parameters.Add("ActorUserId", actorId, DbType.Int32);
        parameters.Add("OptionsJson", JsonSerializer.Serialize(command.Options, Json), DbType.String, size: -1);

        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(
                "dbo.CustomField_Create",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        var definition = await results.ReadSingleAsync<FieldRow>();
        var options = (await results.ReadAsync<FieldOptionRow>()).Select(row => row.ToOption()).ToArray();

        return definition.ToDefinition(options);
    }

    public async Task<IReadOnlyList<FieldDefinition>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("IncludeInactive", includeInactive, DbType.Boolean);

        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(
                "dbo.CustomField_List",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        var definitions = (await results.ReadAsync<FieldRow>()).ToArray();
        var options = (await results.ReadAsync<FieldOptionRow>()).Select(row => row.ToOption()).ToArray();

        return definitions
            .Select(row => row.ToDefinition(options))
            .ToArray()
            .AsReadOnly();
    }

    public async Task<FieldOption> AddOptionAsync(int fieldId, CreateOptionCommand command, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("FieldId", fieldId, DbType.Int32);
        parameters.Add("Label", command.Label, DbType.String, size: -1);
        parameters.Add("SortOrder", command.SortOrder, DbType.Int32);

        var row = await connection.QuerySingleAsync<FieldOptionRow>(
            new CommandDefinition(
                "dbo.CustomField_AddOption",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        return row.ToOption();
    }

    public Task SetFieldActiveAsync(int fieldId, bool active, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("FieldId", fieldId, DbType.Int32);
        parameters.Add("IsActive", active, DbType.Boolean);

        return ExecuteAsync("dbo.CustomField_SetActive", parameters, cancellationToken);
    }

    public Task SetOptionActiveAsync(int fieldId, int optionId, bool active, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("FieldId", fieldId, DbType.Int32);
        parameters.Add("OptionId", optionId, DbType.Int32);
        parameters.Add("IsActive", active, DbType.Boolean);

        return ExecuteAsync("dbo.CustomField_SetOptionActive", parameters, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerFieldState>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("CustomerId", customerId, DbType.Guid);

        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(
                "dbo.CustomerCustomField_ListForCustomer",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        var rows = (await results.ReadAsync<ProfileRow>()).ToArray();
        var options = (await results.ReadAsync<FieldOptionRow>()).Select(row => row.ToOption()).ToArray();

        return rows
            .Select(row => row.ToState(options))
            .ToArray()
            .AsReadOnly();
    }

    public async Task<CustomerFieldWriteResult> SetValueAsync(SetFieldValueCommand command, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("CustomerId", command.CustomerId, DbType.Guid);
        parameters.Add("FieldId", command.FieldId, DbType.Int32);
        parameters.Add("TextValue", command.TextValue, DbType.String, size: -1);
        parameters.Add("SelectedOptionIdsJson", command.SelectedOptionIds is null ? null : JsonSerializer.Serialize(command.SelectedOptionIds), DbType.String, size: -1);
        parameters.Add("ExpectedVersion", command.ExpectedVersion, DbType.Int64);
        parameters.Add("ActorUserId", command.ActorUserId, DbType.Int32);

        var row = await connection.QuerySingleAsync<ValueRow>(
            new CommandDefinition(
                "dbo.CustomerCustomField_SetValue",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        return new(row.ToValue(), row.Changed);
    }

    public async Task<IReadOnlyList<FieldValueVersion>> GetHistoryAsync(Guid customerId, int fieldId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("CustomerId", customerId, DbType.Guid);
        parameters.Add("FieldId", fieldId, DbType.Int32);

        var rows = await connection.QueryAsync<HistoryRow>(
            new CommandDefinition(
                "dbo.CustomerCustomField_GetHistory",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        return rows
            .Select(row => row.ToVersion())
            .ToArray()
            .AsReadOnly();
    }

    private async Task ExecuteAsync(string procedure, DynamicParameters parameters, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                procedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
    }
}
