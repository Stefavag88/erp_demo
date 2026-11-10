using ERP.CustomerCustomFields.Api.Contracts;
using ERP.CustomerCustomFields.Application.Contracts;

namespace ERP.CustomerCustomFields.Api.Endpoints;

public static class CustomFieldEndpoints
{
    public static void MapCustomFieldEndpoints(this WebApplication app)
    {
        var fields = app
            .MapGroup("/api/custom-fields")
            .RequireAuthorization();

        fields.MapGet("", async (bool? includeInactive, ICustomFieldService service, CancellationToken token)
            => Results.Ok(await service.ListAsync(includeInactive, token)));

        fields.MapPost("", async (CreateFieldInput request, ICustomFieldService service, CancellationToken token) =>
        {
            var field = await service.CreateAsync(request, token);
            return Results.Created($"/api/custom-fields/{field.FieldId}", field);
        });

        fields.MapPost("/{fieldId:int}/options", async (int fieldId, CreateOptionCommand request, ICustomFieldService service, CancellationToken token) =>
        {
            var option = await service.AddOptionAsync(fieldId, request, token);
            return Results.Created($"/api/custom-fields/{fieldId}/options/{option.OptionId}", option);
        });

        fields.MapPatch("/{fieldId:int}/active", async (int fieldId, ActiveRequest request, ICustomFieldService service, CancellationToken token) =>
        {
            await service.SetFieldActiveAsync(fieldId, request.IsActive, token);
            return Results.Ok();
        });

        fields.MapPatch("/{fieldId:int}/options/{optionId:int}/active", async (int fieldId, int optionId, ActiveRequest request, ICustomFieldService service, CancellationToken token) =>
        {
            await service.SetOptionActiveAsync(fieldId, optionId, request.IsActive, token);
            return Results.Ok();
        });

        var customers = app.MapGroup("/api/customers").RequireAuthorization();

        customers.MapGet("", async (ICustomerService service, CancellationToken token)
            => Results.Ok(await service.ListAsync(token)));

        customers.MapPost("", async (CreateCustomerRequest request, ICustomerService service, CancellationToken token) =>
        {
            var customer = await service.CreateAsync(request.Name, token);
            return Results.Created($"/api/customers/{customer.CustomerId}", customer);
        });

        customers.MapGet("/{customerId:guid}/custom-fields", async (Guid customerId, ICustomFieldService service, CancellationToken token)
            => Results.Ok(await service.ListForCustomerAsync(customerId, token)));

        customers.MapPut("/{customerId:guid}/custom-fields/{fieldId:int}", async (Guid customerId, int fieldId, SetFieldValueInput request, ICustomFieldService service, CancellationToken token)
            => Results.Ok(await service.SetValueAsync(customerId, fieldId, request, token)));

        customers.MapGet("/{customerId:guid}/custom-fields/{fieldId:int}/history", async (Guid customerId, int fieldId, ICustomFieldService service, CancellationToken token)
            => Results.Ok(await service.GetHistoryAsync(customerId, fieldId, token)));
    }
}
