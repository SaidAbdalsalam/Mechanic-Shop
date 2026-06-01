using MechanicShop.Application.Features.Identity;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<bool> IsInRoleAsync(string userId, string role);
    Task<bool> AuthorizeAsync(string userId, string? policyName);
    Task<Result<UserInformationDto>> AuthenticateAsync(string email, string password);
    Task<Result<Guid>> CreateUserAsync(string email, string password, string role);
    Task<Result<Deleted>> DeleteUserAsync(string userId);
    Task<Result<UserInformationDto>> GetUserByIdAsync(string userId);
    Task<string?> GetUserNameAsync(string userId);
}
