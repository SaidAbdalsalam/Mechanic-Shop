using System.Security.Claims;

namespace MechanicShop.Application.Features.Identity;

public sealed record UserInformationDto(
    string UserId,
    string Email,
    IList<string> Roles,
    IList<Claim> Claims
);
