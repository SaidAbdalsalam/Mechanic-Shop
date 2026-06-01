using System.Security.Claims;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Interfaces;

public interface ITokenProvider
{
    Task<Result<TokenDto>> GenerateJwtTokenAsync(
        UserInformationDto user,
        CancellationToken ct = default
    );

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
