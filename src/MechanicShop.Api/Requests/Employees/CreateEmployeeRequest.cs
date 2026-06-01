using System.ComponentModel.DataAnnotations;
using MechanicShop.Domain.Identity;

namespace MechanicShop.Api.Requests.Employees;

public sealed class CreateEmployeeRequest
{
    [Required(ErrorMessage = "First name is required.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    [EnumDataType(typeof(Role))]
    public Role Role { get; set; }
}
