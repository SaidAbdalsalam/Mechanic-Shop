using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.GetUserInfo;

public sealed class GetUserByIdQueryHandler(
    ILogger<GetUserByIdQueryHandler> logger,
    IIdentityService identityService
) : IRequestHandler<GetUserByIdQuery, Result<UserInformationDto>>
{
    private readonly ILogger<GetUserByIdQueryHandler> _logger = logger;
    private readonly IIdentityService _identityService = identityService;

    public async Task<Result<UserInformationDto>> Handle(
        GetUserByIdQuery request,
        CancellationToken ct
    )
    {
        var getUserByIdResult = await _identityService.GetUserByIdAsync(request.UserId!);

        if (getUserByIdResult.IsError)
        {
            _logger.LogError(
                "User with Id { UserId }{ErrorDetails}",
                request.UserId,
                getUserByIdResult.TopError.Description
            );

            return getUserByIdResult.Errors;
        }

        return getUserByIdResult.Value;
    }
}
