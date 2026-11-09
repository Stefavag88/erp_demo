namespace ERP.Discounts.Application.Repositories;

public interface IPromotionRepository 
{ 
    Task<IReadOnlyList<PromotionDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken);
}
