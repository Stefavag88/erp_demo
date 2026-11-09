using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Validation;
using ERP.CustomerCustomFields.Domain.Enums;

namespace ERP.CustomerCustomFields.UnitTests.Validation;

public class CustomFieldValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_field_keys_are_rejected(string key) => Assert.Throws<CustomFieldException>(() => CustomFieldValidator.Create(new(key, "Label", CustomFieldType.Text, [])));

    [Fact]
    public void Text_cannot_define_options() => Assert.Throws<CustomFieldException>(() => CustomFieldValidator.Create(new("key", "Label", CustomFieldType.Text, [new("Option", 1)])));

    [Fact]
    public void Empty_customer_is_rejected() => Assert.Throws<CustomFieldException>(() => CustomFieldValidator.Value(new(Guid.Empty, 1, "value", null, null, 1)));

    [Fact]
    public void Overlong_text_is_rejected() => Assert.Throws<CustomFieldException>(() => CustomFieldValidator.Value(new(Guid.NewGuid(), 1, new string ('x', 2001), null, null, 1)));

    [Fact]
    public void Duplicated_selections_are_rejected() => Assert.Throws<CustomFieldException>(() => CustomFieldValidator.Value(new(Guid.NewGuid(), 1, null, [1, 1], null, 1)));

    [Fact]
    public void Nonempty_whitespace_is_preserved()
    {
        Assert.Equal("  ", CustomFieldValidator.Value(new(Guid.NewGuid(), 1, "  ", null, null, 1)).TextValue);
    }
}
