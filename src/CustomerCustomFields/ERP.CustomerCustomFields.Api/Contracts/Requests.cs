namespace ERP.CustomerCustomFields.Api.Contracts;

public sealed record CreateCustomerRequest(string Name);
public sealed record ActiveRequest(bool IsActive);
