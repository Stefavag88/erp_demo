using ERP.CustomerCustomFields.Domain.Enums;
using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.UnitTests.Domain;

public class FieldDefinitionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_nonpositive_identity(int id)
        => Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(id, "key", "Label", CustomFieldType.Text, true, []));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\u2003")]
    public void Rejects_blank_keys_and_labels(string? text)
    {
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, text!, "Label", CustomFieldType.Text, true, []));
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, "key", text!, CustomFieldType.Text, true, []));
    }

    [Fact]
    public void Enforces_metadata_lengths_and_field_type()
    {
        var field = new FieldDefinition(1, new string('k', 100), new string('l', 200), CustomFieldType.MultiSelect, false, []);
        Assert.Equal(100, field.FieldKey.Length);
        Assert.Equal(200, field.Label.Length);
        Assert.False(field.IsActive);
        Assert.Empty(field.Options);
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, new string('k', 101), "Label", CustomFieldType.Text, true, []));
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, "key", new string('l', 201), CustomFieldType.Text, true, []));
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, "key", "Label", (CustomFieldType)0, true, []));
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, "key", "Label", (CustomFieldType)99, true, []));
    }

    [Fact]
    public void Rejects_null_foreign_duplicate_and_text_options()
    {
        var option = new FieldOption(1, 1, "Option", 0, true);
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, "key", "Label", CustomFieldType.MultiSelect, true, null!));
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, "key", "Label", CustomFieldType.MultiSelect, true, [null!]));
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(2, "key", "Label", CustomFieldType.MultiSelect, true, [option]));
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, "key", "Label", CustomFieldType.MultiSelect, true, [option, option]));
        Assert.ThrowsAny<ArgumentException>(() => new FieldDefinition(1, "key", "Label", CustomFieldType.Text, true, [option]));
    }

    [Fact]
    public void Owns_an_immutable_option_snapshot_including_inactive_options()
    {
        FieldOption[] options = [new(1, 1, "Original", 0, false)];
        var field = new FieldDefinition(1, "key", "Label", CustomFieldType.MultiSelect, true, options);
        options[0] = new(2, 1, "Replacement", 1, true);

        Assert.Equal("Original", Assert.Single(field.Options).Label);
        Assert.False(field.Options[0].IsActive);
        Assert.Throws<NotSupportedException>(() => ((IList<FieldOption>)field.Options).Clear());
    }
}
