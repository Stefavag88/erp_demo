namespace ERP.Discounts.Application.Repositories;
public interface IPromotionRepository { Task<IReadOnlyList<PromotionDefinition>> GetActiveDefinitionsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken); }
