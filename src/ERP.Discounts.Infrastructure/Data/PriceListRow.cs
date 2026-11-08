using ERP.Discounts.Application.Repositories;

namespace ERP.Discounts.Infrastructure.Data;

internal sealed class PriceListRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string CalculationMethod { get; set; } = "";
    public string CalculationBase { get; set; } = "";
    public decimal Value { get; set; }
    public int PriorityWithinType { get; set; }

    internal PriceListDefinition ToDefinition() => new(Id, Name, DiscountRowMapper.Method(CalculationMethod), DiscountRowMapper.Base(CalculationBase), Value, PriorityWithinType);
}
