using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.UnitTests.Domain;

public class FieldOptionTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Rejects_nonpositive_identifiers(int optionId, int fieldId)
        => Assert.ThrowsAny<ArgumentException>(() => new FieldOption(optionId, fieldId, "Option", 0, true));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void Rejects_blank_labels(string? label)
        => Assert.ThrowsAny<ArgumentException>(() => new FieldOption(1, 2, label!, 0, true));

    [Fact]
    public void Preserves_valid_labels_order_and_inactive_state()
    {
        var label = new string('x', 200);
        var option = new FieldOption(1, 2, label, int.MinValue, false);
        Assert.Equal(1, option.OptionId);
        Assert.Equal(2, option.FieldId);
        Assert.Equal(label, option.Label);
        Assert.Equal(int.MinValue, option.SortOrder);
        Assert.False(option.IsActive);
        Assert.ThrowsAny<ArgumentException>(() => new FieldOption(1, 2, new string('x', 201), 0, true));
    }
}
