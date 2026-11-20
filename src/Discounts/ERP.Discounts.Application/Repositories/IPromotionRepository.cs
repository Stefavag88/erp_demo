namespace ERP.Discounts.Application.Repositories;

public interface IPromotionRepository 
{ 
    Task<IReadOnlyList<PromotionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken);
}
