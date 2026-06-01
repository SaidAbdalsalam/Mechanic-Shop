using MechanicShop.Application.Features.Employees.DTOs;

namespace MechanicShop.Application.Features.Employees.Mapper;

public static class EmployeeMapper
{
    public static EmployeeDto ToDto(this Employee entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new EmployeeDto
        {
            Id = entity.Id,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            FullName = entity.FullName,
            Role = entity.Role.ToString(),
        };
    }

    public static List<EmployeeDto> ToDtos(this List<Employee> entities)
    {
        return [.. entities.Select(e => e.ToDto())];
    }
}
