using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.Scheduling.DTOs;

public sealed record SpotDto(Spot Spot, List<AvailabilitySlotDto> AvailabilitySlots);
