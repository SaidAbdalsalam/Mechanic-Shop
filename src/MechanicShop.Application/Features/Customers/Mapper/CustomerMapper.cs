using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Customers;

namespace MechanicShop.Application.Features.Customers.Mapper;

public static class CustomerMapper
{
    public static CustomerDto ToDto(this Customer entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new CustomerDto(
            entity.Id,
            entity.Name!,
            entity.PhoneNumber!,
            entity.Address!,
            entity.Email!,
            entity.Vehicles.Select(v => v.ToDto()).ToList() ?? []
        );
    }

    public static List<CustomerDto> ToDtos(this IEnumerable<Customer> entities)
    {
        return [.. entities.Select(e => e.ToDto())];
    }

    public static VehicleDto ToDto(this Vehicle entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new VehicleDto(
            entity.CustomerId,
            entity.Id,
            entity.Make,
            entity.Model,
            entity.Year,
            entity.LicensePlate
        );
        ;
    }

    public static List<VehicleDto> ToDtos(this IEnumerable<Vehicle> entities)
    {
        return [.. entities.Select(e => e.ToDto())];
    }
}
