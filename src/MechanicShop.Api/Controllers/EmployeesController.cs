using Asp.Versioning;
using MechanicShop.Application.Features.Employees.Commands;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.Employees.Queries.GetEmployees;
using MechanicShop.Api.Requests.Employees;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace MechanicShop.Api.Controllers;

[Route("api/v{version:apiVersion}/employees")]
[ApiVersion("1.0")]
[Authorize]
public sealed class EmployeesController(ISender sender) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(List<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves the list of available employee definitions.")]
    [EndpointDescription(
        "Returns all employee records associated with the system, accessible only to users with the Manager role."
    )]
    [EndpointName("GetEmployees")]
    [MapToApiVersion("1.0")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> Get([FromQuery] Role? role, CancellationToken ct)
    {
        var result = await sender.Send(new GetEmployeesQuery(role), ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpGet("employees/{employeeId:guid}", Name = "GetEmployeeById")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Get employee by id")]
    [EndpointDescription("Return a dedicated employee by ID")]
    [EndpointName("GetEmployeeById")]
    [MapToApiVersion("1.0")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> GetEmployeeById(Guid employeeId, CancellationToken ct)
    {
        var result = await sender.Send(new GetEmployeeByIdQuery(employeeId), ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Create Employee")]
    [EndpointDescription("Create a new employee in the system with a defined role.")]
    [EndpointName("CreateEmployee")]
    [MapToApiVersion("1.0")]
    [Authorize(Roles = nameof(Role.Manager))]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken ct
    )
    {
        var result = await sender.Send(
            new CreateEmployeeCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password,
                request.Role
            ),
            ct
        );

        return result.Match(
            response =>
                CreatedAtRoute(
                    routeName: "GetEmployeeById",
                    routeValues: new { version = "1.0", EmployeeId = response.Id },
                    value: response
                ),
            Problem
        );
    }
}
