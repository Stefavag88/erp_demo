using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Application.Contracts;

public sealed record CustomerFieldResponse(FieldDefinitionResponse Definition, FieldValue Value);
