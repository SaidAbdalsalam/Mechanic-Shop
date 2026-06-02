using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Domain.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace MechanicShop.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<AppUser> userManager,
    IUserClaimsPrincipalFactory<AppUser> userClaimsPrincipal,
    IAuthorizationService authorizationService
) : IIdentityService
{
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly IUserClaimsPrincipalFactory<AppUser> _userClaimsPrincipal =
        userClaimsPrincipal;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<bool> IsInRoleAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user != null && await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<bool> AuthorizeAsync(string userId, string? policyName)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return false;
        }

        var principle = await _userClaimsPrincipal.CreateAsync(user);
        var result = await _authorizationService.AuthorizeAsync(principle, policyName!);

        return result.Succeeded;
    }

    public async Task<Result<UserInformationDto>> AuthenticateAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            return Error.Conflict("Invalid_Login_Attempt", "Email / Password are incorrect");
        }
        if (!user.EmailConfirmed)
        {
            return Error.Conflict(
                "Email_Not_Confirmed",
                $"email '{UtilityService.MaskEmail(email)}' not confirmed"
            );
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            return Error.Conflict("Invalid_Login_Attempt", "Email / Password are incorrect");
        }

        return new UserInformationDto(
            user.Id,
            email,
            await _userManager.GetRolesAsync(user),
            await _userManager.GetClaimsAsync(user)
        );
    }

    public async Task<Result<UserInformationDto>> GetUserByIdAsync(string userId)
    {
        var user =
            await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException();
        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);

        return new UserInformationDto(user.Id, user.Email!, roles, claims);
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user?.UserName;
    }

    public async Task<Result<Guid>> CreateUserAsync(string email, string password, string role)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            return Error.Conflict("User_Exists", $"User with email: {email} already exists");
        }

        var user = new AppUser { UserName = email, Email = email };

        var createdResult = await _userManager.CreateAsync(user, password);

        if (!createdResult.Succeeded)
        {
            var errors = createdResult
                .Errors.Select(e => Error.Failure(e.Code, e.Description))
                .ToList();

            return errors;
        }

        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _userManager.ConfirmEmailAsync(user, confirmationToken);

        if (!string.IsNullOrEmpty(role))
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        return Guid.Parse(user.Id);
    }

    public async Task<Result<Deleted>> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Result.Deleted;
        }

        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => Error.Failure(e.Code, e.Description)).ToList();

            return errors;
        }

        return Result.Deleted;
    }
}
