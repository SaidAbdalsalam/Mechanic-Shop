namespace MechanicShop.Application.Features.Customers.DTOs;

public sealed record VehicleDto(
    Guid CustomerId,
    Guid VehicleId,
    string Make,
    string Model,
    int Year,
    string LicensePlate
);
