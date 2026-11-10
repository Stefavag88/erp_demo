using System.Security.Claims;
using ERP.CustomerCustomFields.Application.Contracts;

namespace ERP.CustomerCustomFields.Api.Authentication;

public sealed class CurrentActor(IHttpContextAccessor context) : ICurrentActor
{
    public int UserId => int.TryParse(context.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0
        ? id
        : throw new CustomFieldException("A valid actor claim is required.");
}
