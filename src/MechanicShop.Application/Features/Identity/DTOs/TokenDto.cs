namespace MechanicShop.Application.Features.Identity;

public sealed class TokenDto
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
}
