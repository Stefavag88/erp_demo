using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Validation;

namespace ERP.CustomerCustomFields.UnitTests.Validation;

public class RawTextValidationTests
{
    [Fact]
    public void Validation_preserves_supplied_empty_text_for_SQL_field_type_validation()
    {
        var command = new SetFieldValueCommand(Guid.NewGuid(), 1, "", null, null, 1);
        Assert.Equal("", CustomFieldValidator.Value(command).TextValue);
    }
}
