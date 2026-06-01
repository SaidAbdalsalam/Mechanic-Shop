using System.Security.Claims;
using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.RefreshTokens;

public class RefreshTokenQueryHandler(
    ILogger<RefreshTokenQueryHandler> logger,
    IIdentityService identityService,
    IAppDbContext context,
    ITokenProvider tokenProvider,
    TimeProvider timeProvider
) : IRequestHandler<RefreshTokenQuery, Result<TokenDto>>
{
    private readonly ILogger<RefreshTokenQueryHandler> _logger = logger;
    private readonly IIdentityService _identityService = identityService;
    private readonly IAppDbContext _context = context;
    private readonly ITokenProvider _tokenProvider = tokenProvider;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Result<TokenDto>> Handle(RefreshTokenQuery query, CancellationToken ct)
    {
        var principal = _tokenProvider.GetPrincipalFromExpiredToken(query.ExpiredAccessToken);

        if (principal is null)
        {
            _logger.LogError("Expired access token is not valid");
            return ApplicationErrors.ExpiredAccessTokenInvalid;
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        if (userId is null)
        {
            _logger.LogError("Invalid userId claim");

            return ApplicationErrors.UserIdClaimInvalid;
        }

        var getUserResult = await _identityService.GetUserByIdAsync(userId);
        if (getUserResult.IsError)
        {
            return getUserResult.Errors;
        }

        var oldRefreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(
            r => r.Token == query.RefreshToken && r.UserId == userId,
            ct
        );

        if (oldRefreshToken is null || oldRefreshToken.ExpiresOnUtc < _timeProvider.GetUtcNow())
        {
            return ApplicationErrors.RefreshTokenExpired;
        }
        var generateTokenResult = await _tokenProvider.GenerateJwtTokenAsync(
            getUserResult.Value,
            ct
        );
        if (generateTokenResult.IsError)
        {
            return generateTokenResult.Errors;
        }

        _context.RefreshTokens.Remove(oldRefreshToken);

        var newRefreshTokenResult = RefreshToken.Create(
            Guid.NewGuid(),
            generateTokenResult.Value.RefreshToken,
            userId,
            _timeProvider.GetUtcNow().AddDays(7)
        );
        if (newRefreshTokenResult.IsError)
        {
            return newRefreshTokenResult.Errors;
        }
        _context.RefreshTokens.Add(newRefreshTokenResult.Value);
        await _context.SaveChangesAsync(ct);
        return generateTokenResult.Value;
    }
}
