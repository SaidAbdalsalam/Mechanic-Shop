using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.Customers;

public class AddVehicleRequest
{
    [Required(ErrorMessage = "Customer Id is required.")]
    public Guid CustomerId { get; set; }

    [Required(ErrorMessage = "Make is required.")]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = "Model is required.")]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = "Year is required.")]
    public int Year { get; set; }

    [Required(ErrorMessage = "License plate is required.")]
    public string LicensePlate { get; set; } = string.Empty;
}
