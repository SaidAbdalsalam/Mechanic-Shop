using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.AddVehicle;

public sealed record AddVehicleCommand(
    Guid CustomerId,
    string Make,
    string Model,
    int Year,
    string LicensePlate
) : IRequest<Result<VehicleDto>>;
