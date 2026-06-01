using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.GenerateTokens;

public class GenerateTokenQueryHandler(
    IAppDbContext Context,
    ILogger<GenerateTokenQueryHandler> logger,
    IIdentityService identityService,
    ITokenProvider tokenProvider
) : IRequestHandler<GenerateTokenQuery, Result<TokenDto>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<GenerateTokenQueryHandler> _logger = logger;
    private readonly IIdentityService _identityService = identityService;
    private readonly ITokenProvider _tokenProvider = tokenProvider;

    public async Task<Result<TokenDto>> Handle(GenerateTokenQuery query, CancellationToken ct)
    {
        var userResponse = await _identityService.AuthenticateAsync(query.Email, query.Password);

        if (userResponse.IsError)
        {
            return userResponse.Errors;
        }

        var generateTokenResult = await _tokenProvider.GenerateJwtTokenAsync(
            userResponse.Value,
            ct
        );

        if (generateTokenResult.IsError)
        {
            _logger.LogError(
                "Generate token error occurred: {ErrorDescription}",
                generateTokenResult.TopError.Description
            );

            return generateTokenResult.Errors;
        }

        await _context
            .RefreshTokens.Where(rt => rt.UserId == userResponse.Value.UserId)
            .ExecuteDeleteAsync(ct);

        var refreshTokenResult = RefreshToken.Create(
            Guid.NewGuid(),
            generateTokenResult.Value.RefreshToken,
            userResponse.Value.UserId.ToString(),
            DateTimeOffset.UtcNow.AddDays(7)
        );
        if (refreshTokenResult.IsError)
        {
            _logger.LogError("Failed to create RefreshToken entity.");
            return refreshTokenResult.Errors;
        }
        _context.RefreshTokens.Add(refreshTokenResult.Value);
        await _context.SaveChangesAsync(ct);

        return generateTokenResult.Value;
    }
}
