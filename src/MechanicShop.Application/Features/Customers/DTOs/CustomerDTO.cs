namespace MechanicShop.Application.Features.Customers.DTOs;

public sealed record CustomerDto(
    Guid CustomerId,
    string Name,
    string PhoneNumber,
    string Address,
    string Email,
    List<VehicleDto> Vehicles
);
