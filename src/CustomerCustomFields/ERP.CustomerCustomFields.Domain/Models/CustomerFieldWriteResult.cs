namespace ERP.CustomerCustomFields.Domain.Models;

public sealed record CustomerFieldWriteResult(FieldValue Value, bool Changed);
