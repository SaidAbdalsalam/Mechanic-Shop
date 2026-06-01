using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveVehicle;

public sealed record RemoveVehicleCommand(Guid VehicleId) : IRequest<Result<Deleted>>;
