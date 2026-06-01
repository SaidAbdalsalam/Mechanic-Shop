namespace MechanicShop.Application.Features.Scheduling.DTOs;

public sealed record ScheduleDto(DateOnly OnDate, bool EndDay, List<SpotDto> Spots);
