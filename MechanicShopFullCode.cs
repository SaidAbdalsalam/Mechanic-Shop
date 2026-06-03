Total Files: 236
========================

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\DependencyInjection.cs =======

using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using MechanicShop.Api.Infrastructure;
using MechanicShop.Api.OpenApi;
using MechanicShop.Api.Services;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.Settings;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace MechanicShop.Api.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        services
            .AddCustomProblemDetails()
            .AddCustomApiVersioning()
            .AddApiDocumentation()
            .AddExceptionHandling()
            .AddControllerWithJsonConfiguration()
            .AddValidation()
            .AddConfiguredCors(configuration)
            .AddIdentityInfrastructure()
            .AddAppRateLimiting()
            .AddAppOutputCaching()
            .AddAppOpenTelemetry()
            .AddSignalR();

        services.AddAntiforgery();
        return services;
    }

    public static IServiceCollection AddAppOutputCaching(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            options.SizeLimit = 100 * 1024 * 1024; // 100 mb
            options.AddBasePolicy(policy => policy.Expire(TimeSpan.FromSeconds(60)));
        });

        return services;
    }

    public static IServiceCollection AddAppOpenTelemetry(this IServiceCollection services)
    {
        services
            .AddOpenTelemetry()
            .ConfigureResource(res => res.AddService("MechanicShop"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();

                tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();

                metrics.AddOtlpExporter().AddPrometheusExporter();
            });

        return services;
    }

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddSlidingWindowLimiter(
                "SlidingWindow",
                limiterOptions =>
                {
                    limiterOptions.PermitLimit = 100;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.SegmentsPerWindow = 6;
                    limiterOptions.QueueLimit = 10;
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.AutoReplenishment = true;
                }
            );

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }

    public static IServiceCollection AddCustomProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = (context) =>
            {
                context.ProblemDetails.Instance =
                    $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
                context.ProblemDetails.Extensions.Add(
                    "requestId",
                    context.HttpContext.TraceIdentifier
                );
            }
        );

        return services;
    }

    public static IServiceCollection AddCustomApiVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        string[] versions = ["v1"];

        foreach (var version in versions)
        {
            services.AddOpenApi(
                version,
                options =>
                {
                    // Versioning config
                    options.AddDocumentTransformer<VersionInfoTransformer>();

                    // Security Scheme config
                    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();

                    // Security Operation config
                    options.AddOperationTransformer<BearerSecurityOperationTransformer>();
                }
            );
        }

        return services;
    }

    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    public static IServiceCollection AddControllerWithJsonConfiguration(
        this IServiceCollection services
    )
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition =
                    JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        return services;
    }

    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        return services;
    }

    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUser, CurrentUser>();
        services.AddHttpContextAccessor();
        return services;
    }

    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>()!;

        services.AddCors(options =>
            options.AddPolicy(
                appSettings.CorsPolicyName,
                policy =>
                    policy
                        .WithOrigins(appSettings.AllowedOrigins!)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
            )
        );

        return services;
    }

    public static IApplicationBuilder UseCoreMiddlewares(
        this IApplicationBuilder app,
        IConfiguration configuration,
        IWebHostEnvironment env
    )
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        app.UseMiddleware<RequestLogContextMiddleware>();
        if (!env.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }
        app.UseSerilogRequestLogging();
        app.UseRouting();
        app.UseCors(configuration["AppSettings:CorsPolicyName"]!);
        app.UseOutputCache();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Program.cs =======

using MechanicShop.Api.DependencyInjection;
using MechanicShop.Api.Infrastructure;
using MechanicShop.Infrastructure.Data;
using MechanicShop.Infrastructure.RealTime;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddPresentation(builder.Configuration)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Host.UseSerilog(
    (context, LoggerConfig) =>
    {
        LoggerConfig.ReadFrom.Configuration(context.Configuration);
    }
);
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "MechanicShop API V1");
        options.EnableDeepLinking();
        options.DisplayRequestDuration();
        options.EnableFilter();
    });

    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseCoreMiddlewares(builder.Configuration, app.Environment);

app.MapControllers();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapHub<WorkOrderHub>("/hubs/workorders");

await app.Services.InitializeDatabaseAsync();

app.Run();

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Controllers\ApiController.cs =======

using MechanicShop.Domain.Common.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MechanicShop.Api.Controllers;

[ApiController]
public class ApiController : ControllerBase
{
    protected ActionResult Problem(List<Error> errors)
    {
        if (errors.Count is 0)
        {
            return Problem();
        }

        if (errors.All(error => error.Type == ErrorKind.Validation))
        {
            return ValidationProblem(errors);
        }

        return Problem(errors[0]);
    }

    private ObjectResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Unauthorized => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Problem(statusCode: statusCode, title: error.Description);
    }

    private ActionResult ValidationProblem(List<Error> errors)
    {
        var modelStateDictionary = new ModelStateDictionary();

        errors.ForEach(error => modelStateDictionary.AddModelError(error.Code, error.Description));

        return ValidationProblem(modelStateDictionary);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Controllers\CustomersController.cs =======

using Asp.Versioning;
using MechanicShop.Api.Requests.Customers;
using MechanicShop.Application.Features.Customers.Commands.AddVehicle;
using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;
using MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;
using MechanicShop.Application.Features.Customers.Commands.RemoveVehicle;
using MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;
using MechanicShop.Application.Features.Customers.Commands.UpdateVehicle;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Customers.GetAllCustomers.Queries;
using MechanicShop.Application.Features.Customers.Queries.GetCustomerById;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace MechanicShop.Api.Controllers;

[Route("api/v{version:apiVersion}/customers")]
[ApiVersion("1.0")]
[Authorize]
public sealed class CustomersController(ISender sender) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(List<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves a list of customers.")]
    [EndpointDescription("Returns all customers associated with the current user.")]
    [EndpointName("GetCustomers")]
    [MapToApiVersion("1.0")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await sender.Send(new GetCustomersQuery(), ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpGet("{customerId:guid}", Name = "GetCustomerById")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves a customer by ID.")]
    [EndpointDescription("Returns detailed information about the specified customer if found.")]
    [EndpointName("GetCustomerById")]
    [MapToApiVersion("1.0")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> GetById(Guid customerId, CancellationToken ct)
    {
        var result = await sender.Send(new GetCustomerByIdQuery(customerId), ct);
        return result.Match(response => Ok(response), Problem);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Creates a new customer.")]
    [EndpointDescription("Adds a new customer to the system.")]
    [EndpointName("CreateCustomer")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken ct
    )
    {
        var vehicles = request.Vehicles.ConvertAll(v => new CreateVehicleDto(
            v.Make,
            v.Model,
            v.Year,
            v.LicensePlate
        ));

        var result = await sender.Send(
            new CreateCustomerCommand(
                request.Name,
                request.PhoneNumber,
                request.Email,
                request.Address,
                vehicles
            ),
            ct
        );

        return result.Match(
            response =>
                CreatedAtRoute(
                    routeName: "GetCustomerById",
                    routeValues: new { version = "1.0", customerId = response.CustomerId },
                    value: response
                ),
            Problem
        );
    }

    [HttpPut("{customerId:guid}")]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Updates an existing customer.")]
    [EndpointDescription("Updates a customer and its associated vehicle.")]
    [EndpointName("UpdateCustomer")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Update(
        Guid customerId,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken ct
    )
    {
        var vehicles = request.Vehicles.ConvertAll(v => new UpdateVehicleCommand(
            v.VehicleId,
            v.Make,
            v.Model,
            v.Year,
            v.LicensePlate
        ));

        var command = new UpdateCustomerCommand(
            customerId,
            request.Name,
            request.Email,
            request.PhoneNumber,
            request.Address
        );

        var result = await sender.Send(command, ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpDelete("{customerId:guid}")]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Removes a customer.")]
    [EndpointDescription("Deletes the specified customer from the system.")]
    [EndpointName("RemoveCustomer")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> DeleteCustomer(Guid customerId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveCustomerCommand(customerId), ct);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPost("vehicles")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Add a new vehicle.")]
    [EndpointDescription("Adding a new car for the customer.")]
    [EndpointName("AddVehicles")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> AddVehicles(
        [FromBody] AddVehicleRequest request,
        CancellationToken ct
    )
    {
        var result = await sender.Send(
            new AddVehicleCommand(
                request.CustomerId,
                request.Make,
                request.Model,
                request.Year,
                request.LicensePlate
            ),
            ct
        );

        return result.Match(
            response =>
                CreatedAtRoute(
                    routeName: "GetCustomerById",
                    routeValues: new { version = "1.0", customerId = response.CustomerId },
                    value: response
                ),
            Problem
        );
    }

    [HttpDelete("vehicles/{vehicleId:guid}")]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Removes a vehicle.")]
    [EndpointDescription("Deletes the specified vehicle from the system.")]
    [EndpointName("RemoveVehicle")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> DeleteVehicle(Guid vehicleId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveVehicleCommand(vehicleId), ct);

        return result.Match(_ => NoContent(), Problem);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Controllers\DashboardController.cs =======

using Asp.Versioning;
using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderStats;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MechanicShop.Api.Controllers;

[ApiVersion("1.0")]
[Authorize(Roles = nameof(Role.Manager))]
[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController(ISender sender) : ApiController
{
    [HttpGet("stats")]
    [ProducesResponseType(typeof(TodayWorkOrderStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTodayStats([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var statsDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await sender.Send(
            new GetWorkOrderStatsQuery(statsDate, TimeZoneInfo.Local),
            ct
        );

        return result.Match(response => Ok(response), Problem);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Controllers\EmployeesController.cs =======

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

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Controllers\IdentityController.cs =======

using System.Security.Claims;
using Asp.Versioning;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Application.Features.Identity.Queries.GenerateTokens;
using MechanicShop.Application.Features.Identity.Queries.GetUserInfo;
using MechanicShop.Application.Features.Identity.Queries.RefreshTokens;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MechanicShop.Api.Controllers;

[Route("identity")]
[ApiVersionNeutral]
public sealed class IdentityController(ISender sender) : ApiController
{
    [HttpPost("token/generate")]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Generates an access and refresh token for a valid user.")]
    [EndpointDescription(
        "Authenticates a user using provided credentials and returns a JWT token pair."
    )]
    [EndpointName("GenerateToken")]
    public async Task<IActionResult> GenerateToken(
        [FromBody] GenerateTokenQuery request,
        CancellationToken ct
    )
    {
        var result = await sender.Send(request, ct);
        return result.Match(response => Ok(response), Problem);
    }

    [HttpPost("token/refresh-token")]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Refreshes access token using a valid refresh token.")]
    [EndpointDescription(
        "Exchanges an expired access token and a valid refresh token for a new token pair."
    )]
    [EndpointName("RefreshToken")]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenQuery request,
        CancellationToken ct
    )
    {
        var result = await sender.Send(request, ct);
        return result.Match(response => Ok(response), Problem);
    }

    [HttpGet("current-user/claims")]
    [Authorize]
    [ProducesResponseType(typeof(UserInformationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Gets the current authenticated user's info.")]
    [EndpointDescription(
        "Returns user information for the currently authenticated user based on the access token."
    )]
    [EndpointName("GetCurrentUserClaims")]
    public async Task<IActionResult> GetCurrentUserInfo(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await sender.Send(new GetUserByIdQuery(userId), ct);

        return result.Match(response => Ok(response), Problem);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Controllers\InvoicesController.cs =======

using Asp.Versioning;
using MechanicShop.Application.Features.Invoices.Commands.IssueInvoice;
using MechanicShop.Application.Features.Invoices.Commands.SettleInvoice;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;
using MechanicShop.Application.Features.Invoices.Queries.GetInvoicePdf;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MechanicShop.Api.Controllers;

[Route("api/v{version:apiVersion}/invoices")]
[ApiVersion("1.0")]
[Authorize(Policy = "ManagerOnly")]
public sealed class InvoicesController(ISender sender) : ApiController
{
    [HttpPost("workorders/{workOrderId:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Issues an invoice for a work order.")]
    [EndpointDescription(
        "Creates a new invoice for the specified work order and returns the created invoice resource."
    )]
    [EndpointName("IssueInvoiceForWorkOrder")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> IssueInvoice(
        Guid workOrderId,
        [FromQuery] decimal discount,
        CancellationToken ct
    )
    {
        var command = new IssueInvoiceCommand(workOrderId, discount);
        var result = await sender.Send(command, ct);

        return result.Match(
            response =>
                CreatedAtAction(
                    nameof(GetInvoice),
                    new { version = "1.0", invoiceId = response.InvoiceId },
                    response
                ),
            Problem
        );
    }

    [HttpGet("{invoiceId:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves an invoice by ID.")]
    [EndpointDescription(
        "Returns detailed information about the specified invoice. Only users with the Manager role are authorized."
    )]
    [EndpointName("GetInvoice")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetInvoice(Guid invoiceId, CancellationToken ct)
    {
        var result = await sender.Send(new GetInvoiceByIdQuery(invoiceId), ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpGet("{invoiceId:guid}/pdf")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Downloads the invoice as a PDF file.")]
    [EndpointDescription(
        "Returns the invoice PDF file for the specified invoice ID. Only users with the Manager role are authorized."
    )]
    [EndpointName("GetInvoicePdf")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetInvoicePdf(Guid invoiceId, CancellationToken ct)
    {
        var result = await sender.Send(new GetInvoicePdfQuery(invoiceId), ct);

        return result.Match(
            response => File(response.Content!, "application/pdf", response.FileName),
            Problem
        );
    }

    [HttpPut("{invoiceId:guid}/payments")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Marks an invoice as paid.")]
    [EndpointDescription(
        "Settles the specified invoice. Only users with the Manager role are authorized to perform this operation."
    )]
    [EndpointName("SettleInvoice")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> SettleInvoice(Guid invoiceId, CancellationToken ct)
    {
        var command = new SettleInvoiceCommand(invoiceId);

        var result = await sender.Send(command, ct);

        return result.Match(_ => NoContent(), Problem);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Controllers\RepairTasksController.cs =======

using Asp.Versioning;
using MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;
using MechanicShop.Application.Features.RepairTasks.Command.RemoveRepairTask;
using MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;
using MechanicShop.Api.Requests.RepairTasks;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.RepairTasks.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace MechanicShop.Api.Controllers;

[Route("api/v{version:apiVersion}/repair-tasks")]
[ApiVersion("1.0")]
[Authorize]
public sealed class RepairTasksController(ISender sender) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(List<RepairTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves all repair tasks.")]
    [EndpointDescription("Returns a list of all repair tasks available in the system.")]
    [EndpointName("GetRepairTasks")]
    [MapToApiVersion("1.0")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await sender.Send(new GetRepairTasksQuery(), ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpGet("{repairTaskId:guid}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(RepairTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves a repair task by ID.")]
    [EndpointDescription(
        "Returns detailed information for the specified repair task if it exists."
    )]
    [EndpointName("GetRepairTaskById")]
    [MapToApiVersion("1.0")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> GetById(Guid repairTaskId, CancellationToken ct)
    {
        var result = await sender.Send(new GetRepairTaskByIdQuery(repairTaskId), ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpPost]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(typeof(RepairTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Creates a new repair task.")]
    [EndpointDescription("Creates a repair task and optionally includes parts.")]
    [EndpointName("CreateRepairTask")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Create(
        [FromBody] CreateRepairTaskRequest request,
        CancellationToken ct
    )
    {
        var parts = request.Parts.ConvertAll(p => new CreatePartsDto(p.Name, p.Cost, p.Quantity));

        var command = new CreateRepairTaskCommand(
            request.Name,
            (RepairDurationInMinutes)request.EstimatedDurationInMins,
            request.LaborCost,
            parts
        );

        var result = await sender.Send(command, ct);

        return result.Match(
            response =>
                CreatedAtAction(
                    nameof(GetById),
                    new { repairTaskId = response.RepairTaskId },
                    response
                ),
            Problem
        );
    }

    [HttpPut("{repairTaskId:guid}")]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(typeof(RepairTaskDto), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Updates an existing repair task.")]
    [EndpointDescription("Updates a repair task and its associated parts.")]
    [EndpointName("UpdateRepairTask")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Update(
        Guid repairTaskId,
        [FromBody] UpdateRepairTaskRequest request,
        CancellationToken ct
    )
    {
        var parts = request.Parts.ConvertAll(p => new UpdatePartsDto(
            p.PartId,
            p.Name,
            p.Cost,
            p.Quantity
        ));

        var command = new UpdateRepairTaskCommand(
            repairTaskId,
            request.Name,
            (RepairDurationInMinutes)request.EstimatedDurationInMins,
            request.LaborCost,
            parts
        );

        var result = await sender.Send(command, ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpDelete("{repairTaskId:guid}")]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Removes a repair task.")]
    [EndpointDescription("Deletes the specified repair task from the system.")]
    [EndpointName("RemoveRepairTask")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Delete(Guid repairTaskId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveRepairTaskCommand(repairTaskId), ct);

        return result.Match(_ => NoContent(), Problem);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Controllers\SettingsController.cs =======

using Asp.Versioning;
using MechanicShop.Api.Responses;
using MechanicShop.Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MechanicShop.Api.Controllers;

[Route("api/settings")]
[ApiVersionNeutral]
public sealed class SettingsController(IOptions<AppSettings> options) : ApiController
{
    private readonly AppSettings _settings = options.Value;

    [HttpGet("operating-hours")]
    [ProducesResponseType(typeof(OperatingHoursResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Gets the application's operating hours.")]
    [EndpointDescription("Returns the current configured opening and closing times.")]
    [EndpointName("GetOperatingHours")]
    public IActionResult GetOperatingHours()
    {
        return Ok(new OperatingHoursResponse(_settings.OpeningTime, _settings.ClosingTime));
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Controllers\WorkOrdersController.cs =======

using Asp.Versioning;
using MechanicShop.Api.Requests.WorkOrders;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Scheduling.DTOs;
using MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery;
using MechanicShop.Application.Features.WorkOrders.Commands.AssignLabor;
using MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;
using MechanicShop.Application.Features.WorkOrders.Commands.RelocateWorkOrder;
using MechanicShop.Application.Features.WorkOrders.Commands.RemoveWorkOrder;
using MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;
using MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderState;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;
using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MechanicShop.Api.Controllers;

[Route("api/v{version:apiVersion}/workorders")]
[ApiVersion("1.0")]
[Authorize]
public sealed class WorkOrdersController(ISender sender) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<WorkOrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves a paginated list of work orders.")]
    [EndpointDescription(
        "Supports filtering by date range, status, vehicle, labor, spot, and searching by term. Pagination and sorting are supported."
    )]
    [EndpointName("GetWorkOrders")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Get(
        [FromQuery] WorkOrderFilterRequest filters,
        [FromQuery] PageRequest pageRequest,
        CancellationToken ct
    )
    {
        if (pageRequest.Page <= 0)
        {
            return BadRequest("Page must be greater than 0");
        }

        if (pageRequest.PageSize <= 0 || pageRequest.PageSize > 100)
        {
            return BadRequest("PageSize must be between 1 and 100");
        }

        var query = new GetWorkOrdersQuery(
            pageRequest.Page,
            pageRequest.PageSize,
            filters.SearchTerm,
            filters.SortColumn,
            filters.SortDirection,
            filters.State is not null ? (WorkOrderState)(int)filters.State.Value : null,
            filters.VehicleId,
            filters.LaborId,
            filters.StartDateFrom,
            filters.StartDateTo,
            filters.EndDateFrom,
            filters.EndDateTo,
            filters.Spot is not null ? (Spot)(int)filters.Spot.Value : null
        );

        var result = await sender.Send(query, ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpGet("{workOrderId:guid}", Name = "GetWorkOrderById")]
    [ProducesResponseType(typeof(WorkOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves a work order by its ID.")]
    [EndpointDescription(
        "Returns detailed information about the specified work order if it exists."
    )]
    [EndpointName("GetWorkOrderById")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetById(Guid workOrderId, CancellationToken ct)
    {
        var result = await sender.Send(new GetWorkOrderByIdQuery(workOrderId), ct);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(WorkOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Creates a new work order.")]
    [EndpointDescription(
        "Creates a new work order for a vehicle, specifying labor, tasks, and other required information."
    )]
    [EndpointName("CreateWorkOrder")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkOrderRequest request,
        CancellationToken ct
    )
    {
        var result = await sender.Send(
            new CreateWorkOrderCommand(
                request.StartAtUtc,
                request.LaborId,
                request.VehicleId,
                (Spot)(int)request.Spot,
                request.RepairTaskIds
            ),
            ct
        );

        return result.Match(
            response =>
                CreatedAtRoute(
                    routeName: "GetWorkOrderById",
                    routeValues: new { version = "1.0", workOrderId = response.WorkOrderId },
                    value: response
                ),
            Problem
        );
    }

    [HttpPut("{workOrderId:guid}/relocation")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Relocates a work order to a new time and spot.")]
    [EndpointDescription(
        "Updates the scheduled time and assigned bay for a work order. Only users with the Manager role can perform this action."
    )]
    [EndpointName("RescheduleWorkOrder")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Relocate(
        Guid workOrderId,
        RelocateWorkOrderRequest request,
        CancellationToken ct
    )
    {
        var command = new RelocateWorkOrderCommand(
            workOrderId,
            request.NewStartAtUtc,
            (Spot)(int)request.NewSpot
        );

        var result = await sender.Send(command, ct);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPut("{workOrderId:guid}/labor")]
    [Authorize(Policy = "ManagerOnly")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Assigns a labor to a work order.")]
    [EndpointDescription(
        "Associates a labor definition with a specific work order. Only managers can perform this operation."
    )]
    [EndpointName("AssignLaborToWorkOrder")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> AssignLabor(
        Guid workOrderId,
        AssignLaborRequest request,
        CancellationToken ct
    )
    {
        var command = new AssignLaborCommand(workOrderId, Guid.Parse(request.LaborId));

        var result = await sender.Send(command, ct);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPut("{workOrderId:guid}/state")]
    [Authorize(
        Roles = $"{nameof(Role.Manager)},{nameof(Role.Labor)}",
        Policy = "SelfScopedWorkOrderAccess"
    )]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Changes the state of a work order.")]
    [EndpointDescription(
        "Updates the current state of the specified work order. Only users with the Manager role are authorized."
    )]
    [EndpointName("UpdateWorkOrderState")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> UpdateState(
        Guid workOrderId,
        UpdateWorkOrderStateRequest request,
        CancellationToken ct
    )
    {
        var command = new UpdateWorkOrderStateCommand(
            workOrderId,
            (WorkOrderState)(int)request.State
        );

        var result = await sender.Send(command, ct);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPut("{workOrderId:guid}/repair-task")]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRepairTasks(
        Guid workOrderId,
        ModifyRepairTaskRequest request,
        CancellationToken ct
    )
    {
        var command = new UpdateWorkOrderRepairTasksCommand(workOrderId, request.RepairTaskIds);

        var result = await sender.Send(command, ct);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpDelete("{workOrderId:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Deletes a work order.")]
    [EndpointDescription(
        "Deletes the specified work order permanently. Only users with the Manager role are authorized."
    )]
    [EndpointName("DeleteWorkOrder")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Delete(Guid workOrderId, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteWorkOrderCommand(workOrderId), ct);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpGet("schedule/{date}")]
    [Authorize]
    [ProducesResponseType(typeof(ScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves the schedule for a given day.")]
    [EndpointDescription(
        "Returns a schedule view for the specified date. If no date is provided, today's schedule is returned. You can optionally filter by labor ID."
    )]
    [EndpointName("GetDailySchedule")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetSchedule(
        DateOnly? date,
        [FromQuery] Guid? laborId,
        [FromHeader(Name = "X-TimeZone")] string? tz,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(tz))
        {
            return Problem(
                detail: "Missing time zone in 'X-TimeZone' header.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Time Zone Required"
            );
        }

        TimeZoneInfo timeZone;

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(tz);
        }
        catch
        {
            return Problem(
                detail: $"Invalid or unknown time zone: '{tz}'.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Time Zone"
            );
        }

        var scheduleDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await sender.Send(
            new GetDailyScheduleQuery(timeZone, scheduleDate, laborId),
            ct
        );

        return result.Match(response => Ok(response), Problem);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Infrastructure\GlobalExceptionHandler.cs =======

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MechanicShop.Api.Infrastructure;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService = problemDetailsService;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Type = exception.GetType().Name,
                    Title = "Application Error",
                    Detail = exception.Message,
                },
            }
        );
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Infrastructure\RequestLogContextMiddleware.cs =======

using Serilog.Context;

namespace MechanicShop.Api.Infrastructure;

public sealed class RequestLogContextMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        using (LogContext.PushProperty("CorrelationId", context.TraceIdentifier))
        {
            return _next(context);
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\OpenApi\BearerSecurityOperationTransformer.cs =======

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MechanicShop.Api.OpenApi;

public sealed class BearerSecurityOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var hasAuthorize = metadata.OfType<AuthorizeAttribute>().Any();
        var hasAllowAnonymous = metadata.OfType<AllowAnonymousAttribute>().Any();

        if (!hasAuthorize || hasAllowAnonymous)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];

        operation.Security.Add(
            new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecuritySchemeReference(
                        JwtBearerDefaults.AuthenticationScheme,
                        context.Document
                    )
                ] = [],
            }
        );
        return Task.CompletedTask;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\OpenApi\BearerSecuritySchemeTransformer.cs =======

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MechanicShop.Api.OpenApi;

public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Components ??= new OpenApiComponents();

        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "JWT Authorization header using Bearer scheme.",
            };
        return Task.CompletedTask;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\OpenApi\VersionInfoTransformer.cs =======

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MechanicShop.Api.OpenApi;

public sealed class VersionInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var version = context.DocumentName;
        document.Info.Version = version;
        document.Info.Title = $"MechanicShop API {version}";

        return Task.CompletedTask;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\Customers\AddVehicleRequest.cs =======

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

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\Customers\CreateCustomerRequest.cs =======

using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.Customers;

public class CreateCustomerRequest
{
    [Required(ErrorMessage = "Name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "PhoneNumber is required.")]
    [RegularExpression(
        @"^\+?\d{7,15}$",
        ErrorMessage = "Phone number must be 7–15 digits and may start with '+'."
    )]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is invalid.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    public string Address { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "At least one vehicle is required.")]
    public List<CreateVehicleRequest> Vehicles { get; set; } = [];
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\Customers\CreateVehicleRequest.cs =======

using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.Customers;

public class CreateVehicleRequest
{
    [Required(ErrorMessage = "Make is required.")]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = "Model is required.")]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = "Year is required.")]
    public int Year { get; set; }

    [Required(ErrorMessage = "Spot is required.")]
    public string LicensePlate { get; set; } = string.Empty;
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\Customers\UpdateCustomerRequest.cs =======

using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.Customers;

public class UpdateCustomerRequest
{
    [Required(ErrorMessage = "Name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "PhoneNumber is required.")]
    [RegularExpression(
        @"^\+?\d{7,15}$",
        ErrorMessage = "Phone number must be 7–15 digits and may start with '+'."
    )]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is invalid.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    public string Address { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "At least one vehicle is required.")]
    public List<UpdateVehicleRequest> Vehicles { get; set; } = [];
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\Customers\UpdateVehicleRequest.cs =======

using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.Customers;

public class UpdateVehicleRequest
{
    public Guid VehicleId { get; set; }

    [Required(ErrorMessage = "Make is required.")]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = "Model is required.")]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = "Year is required.")]
    public int Year { get; set; }

    [Required(ErrorMessage = "Spot is required.")]
    public string LicensePlate { get; set; } = string.Empty;
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\Employees\CreateEmployeeRequest.cs =======

using System.ComponentModel.DataAnnotations;
using MechanicShop.Domain.Identity;

namespace MechanicShop.Api.Requests.Employees;

public sealed class CreateEmployeeRequest
{
    [Required(ErrorMessage = "First name is required.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    [EnumDataType(typeof(Role))]
    public Role Role { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\RepairTasks\CreateRepairTaskPartRequest.cs =======

using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.RepairTasks;

public class CreateRepairTaskPartRequest
{
    [Required(ErrorMessage = "Part name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cost is required.")]
    [Range(1, 10000, ErrorMessage = "Cost must be between 1 and 10,000.")]
    public decimal Cost { get; set; }

    [Required(ErrorMessage = "Quantity is required.")]
    [Range(1, 10, ErrorMessage = "Quantity must be between 1 and 10.")]
    public int Quantity { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\RepairTasks\CreateRepairTaskRequest.cs =======

using System.ComponentModel.DataAnnotations;
using MechanicShop.Domain.RepairTasks.Enums;

namespace MechanicShop.Api.Requests.RepairTasks;

public class CreateRepairTaskRequest
{
    [Required(ErrorMessage = "Task name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Labor cost is required.")]
    [Range(1, 10000, ErrorMessage = "Labor cost must be between 1 and 10,000.")]
    public decimal LaborCost { get; set; }

    [Required(ErrorMessage = "Estimated duration is required.")]
    public RepairDurationInMinutes EstimatedDurationInMins { get; set; }

    [MinLength(1, ErrorMessage = "At least one part is required.")]
    public List<CreateRepairTaskPartRequest> Parts { get; set; } = [];
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\RepairTasks\UpdateRepairTaskPartRequest.cs =======

using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.RepairTasks;

public class UpdateRepairTaskPartRequest
{
    public Guid? PartId { get; set; }

    [Required(ErrorMessage = "Part name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cost is required.")]
    [Range(1, 10000, ErrorMessage = "Cost must be between 1 and 10,000.")]
    public decimal Cost { get; set; }

    [Required(ErrorMessage = "Quantity is required.")]
    [Range(1, 10, ErrorMessage = "Quantity must be between 1 and 10.")]
    public int Quantity { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\RepairTasks\UpdateRepairTaskRequest.cs =======

using System.ComponentModel.DataAnnotations;
using MechanicShop.Domain.RepairTasks.Enums;

namespace MechanicShop.Api.Requests.RepairTasks;

public class UpdateRepairTaskRequest
{
    [Required(ErrorMessage = "Task name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Labor cost is required.")]
    [Range(1, 10000, ErrorMessage = "Labor cost must be between 1 and 10,000.")]
    public decimal LaborCost { get; set; }

    [Required(ErrorMessage = "Estimated duration is required.")]
    public RepairDurationInMinutes EstimatedDurationInMins { get; set; }

    [MinLength(1, ErrorMessage = "At least one part is required.")]
    public List<UpdateRepairTaskPartRequest> Parts { get; set; } = [];
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\WorkOrders\AssignLaborRequest.cs =======

using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.WorkOrders;

public class AssignLaborRequest
{
    [Required(ErrorMessage = "LaborId is required.")]
    public string LaborId { get; set; } = string.Empty;
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\WorkOrders\CreateWorkOrderRequest.cs =======

using System.ComponentModel.DataAnnotations;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Api.Requests.WorkOrders;

public class CreateWorkOrderRequest
{
    [Required(ErrorMessage = "Spot is required.")]
    [Range(0, 3, ErrorMessage = "Invalid range [0, 1, 2 or 3]")]
    public Spot Spot { get; set; }

    [Required(ErrorMessage = "Vehicle is required.")]
    public Guid VehicleId { get; set; }

    [MinLength(1, ErrorMessage = "At least one repair task must be selected.")]
    public List<Guid> RepairTaskIds { get; set; } = [];

    [Required(ErrorMessage = "Labor is required.")]
    public Guid LaborId { get; set; }

    [Required(ErrorMessage = "StartAt is required.")]
    public DateTimeOffset StartAtUtc { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\WorkOrders\LoginRequest.cs =======

using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.WorkOrders;

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(
        100,
        MinimumLength = 6,
        ErrorMessage = "Password must be at least 6 characters long."
    )]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\WorkOrders\ModifyRepairTaskRequest.cs =======

namespace MechanicShop.Api.Requests.WorkOrders;

public class ModifyRepairTaskRequest
{
    public Guid[] RepairTaskIds { get; set; } = [];
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\WorkOrders\PageRequest.cs =======

namespace MechanicShop.Api.Requests.WorkOrders;

public record PageRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\WorkOrders\RelocateWorkOrderRequest.cs =======

using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Api.Requests.WorkOrders;

public class RelocateWorkOrderRequest
{
    public DateTimeOffset NewStartAtUtc { get; set; }
    public Spot NewSpot { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\WorkOrders\UpdateWorkOrderStateRequest.cs =======

using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Api.Requests.WorkOrders;

public class UpdateWorkOrderStateRequest
{
    public WorkOrderState State { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Requests\WorkOrders\WorkOrderFilterRequest.cs =======

using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Api.Requests.WorkOrders;

public record WorkOrderFilterRequest
{
    public string? SearchTerm { get; set; }
    public string SortColumn { get; set; } = "createdAt";
    public string SortDirection { get; set; } = "desc";
    public WorkOrderState? State { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? LaborId { get; set; }
    public DateTime? StartDateFrom { get; set; }
    public DateTime? StartDateTo { get; set; }
    public DateTime? EndDateFrom { get; set; }
    public DateTime? EndDateTo { get; set; }
    public Spot? Spot { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Responses\OperatingHoursResponse.cs =======

namespace MechanicShop.Api.Responses;

public sealed record OperatingHoursResponse(TimeOnly OpeningTime, TimeOnly ClosingTime);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Api\Services\CuurentUser.cs =======

using System.Security.Claims;
using MechanicShop.Application.Common.Interfaces;

namespace MechanicShop.Api.Services;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? Id =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\DependencyInjection.cs =======

using System.Reflection;
using FluentValidation;
using MechanicShop.Application.Common.behaviors;
using MechanicShop.Application.Common.Behaviors;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CacheQueryBehavior<,>));
        });

        return services;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\behaviors\CacheQueryBehavior.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results.Abstraction;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Common.behaviors;

public sealed class CacheQueryBehavior<TRequest, TResponse>(
    HybridCache Cache,
    ILogger<CacheQueryBehavior<TRequest, TResponse>> Logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly HybridCache _cache = Cache;
    private readonly ILogger<CacheQueryBehavior<TRequest, TResponse>> _logger = Logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct
    )
    {
        if (request is not ICachedQuery cacheRequest)
        {
            return await next(ct);
        }
        _logger.LogInformation("Checking cache for {RequestName}", typeof(TRequest).Name);

        return await _cache.GetOrCreateAsync(
            cacheRequest.CacheKey,
            async factory => await next(ct),
            options: new HybridCacheEntryOptions { Expiration = cacheRequest.Expiration },
            tags: cacheRequest.Tags,
            cancellationToken: ct
        );
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\behaviors\LoggingBehavior.cs =======

using MechanicShop.Application.Common.Interfaces;
using MediatR.Pipeline;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Common.behaviors;

public sealed class LoggingBehavior<TRequest>(ILogger<TRequest> Logger, IUser User)
    : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger = Logger;
    private readonly IUser _user = User;

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        var userId = _user.Id ?? "Anonymous";

        _logger.LogInformation(
            "MechanicShop Request: {Name} | UserId: {@UserId} | Payload: {@Request}",
            requestName,
            userId,
            request
        );

        return Task.CompletedTask;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\behaviors\PerformanceBehavior.cs =======

using System.Diagnostics;
using MechanicShop.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Common.behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<TRequest> logger,
    IUser user,
    IIdentityService identityService
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly Stopwatch _timer = new Stopwatch();
    private readonly ILogger<TRequest> _logger = logger;
    private readonly IUser _user = user;
    private readonly IIdentityService _identityService = identityService;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct
    )
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;

        if (elapsedMilliseconds > 500)
        {
            var requestName = typeof(TRequest).Name;
            var userId = _user.Id ?? string.Empty;
            var userName = string.Empty;

            if (!string.IsNullOrEmpty(userId))
            {
                userName = await _identityService.GetUserNameAsync(userId);
            }

            _logger.LogWarning(
                "Long Running Request: {Name} ({ElapsedMilliseconds} milliseconds) {@UserId} {@UserName} {@Request}",
                requestName,
                elapsedMilliseconds,
                userId,
                userName,
                request
            );
        }

        return response;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\behaviors\UnhandledExceptionBehavior.cs =======

using MediatR;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Common.behaviors;

public sealed class UnhandledExceptionBehavior<TRequest, TResponse>(ILogger<TRequest> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct
    )
    {
        try
        {
            return await next(ct);
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogError(
                ex,
                "Request: Unhandled Exception for Request {Name} {@Request}",
                requestName,
                request
            );

            throw;
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\behaviors\ValidationBehavior.cs =======

using FluentValidation;
using MechanicShop.Application.Common.Errors;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Common.Results.Abstraction;
using MediatR;

namespace MechanicShop.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct
    )
    {
        if (!validators.Any())
        {
            return await next(ct);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));

        var errors = results
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .Select(failure => Error.Validation(failure.ErrorCode, failure.ErrorMessage))
            .Distinct()
            .ToList();

        if (errors.Count > 0)
        {
            return (dynamic)errors;
        }

        return await next(ct);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Errors\ApplicationErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Errors;

public static class ApplicationErrors
{
    public static Error WorkOrderOutsideOperatingHour(
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc
    ) =>
        Error.Conflict(
            "ApplicationErrors.WorkOrder.Outside.OperatingHours",
            $"The WorkOrder time ({startAtUtc} ? {endAtUtc}) is outside of store operating hours."
        );

    public static Error WorkOrderNotFound =>
        Error.NotFound("ApplicationErrors.WorkOrder.NotFound", "WorkOrder does not exist.");

    public static Error LaborOccupied =>
        Error.Conflict(
            "Employee.LaborOccupied",
            "Labor is already occupied during the requested time."
        );

    public static Error CustomerNotFound =>
        Error.NotFound("ApplicationErrors.Customer.NotFound", "Customer does not exist.");

    public static Error VehicleNotFound =>
        Error.NotFound("ApplicationErrors.Vehicle.NotFound", "Vehicle does not exist.");

    public static Error VehicleAlreadyExists =>
        Error.Conflict(
            "ApplicationErrors.Vehicle.AlreadyExists",
            "The license plate is already registered in the system."
        );

    public static Error VehicleSchedulingConflict =>
        Error.Conflict(
            "Vehicle_Overlapping_WorkOrder",
            "The vehicle already has an overlapping WorkOrder."
        );

    public static Error RepairTaskNotFound =>
        Error.NotFound("RepairTask.NotFound", "Repair task does not exist.");

    public static Error WorkOrderMustBeCompletedForInvoicing =>
        Error.Conflict(
            "WorkOrder.InvoiceIssuance.InvalidState",
            "WorkOrder must be in 'Completed' state to issue an invoice."
        );

    public static Error InvoiceNotFound =>
        Error.NotFound("ApplicationErrors.Invoice.NotFound", "Invoice does not exist.");

    public static Error EmployeeNotFound =>
        Error.NotFound("ApplicationErrors.Employee.NotFound", "Employee does not exist.");

    public static Error InvalidRefreshToken =>
        Error.Validation("RefreshToken.Expiry.Invalid", "Expiry must be in the future.");

    public static Error ExpiredAccessTokenInvalid =>
        Error.Conflict(
            code: "Auth.ExpiredAccessToken.Invalid",
            description: "Expired access token is not valid."
        );

    public static Error UserIdClaimInvalid =>
        Error.Conflict(code: "Auth.UserIdClaim.Invalid", description: "Invalid userId claim.");

    public static Error RefreshTokenExpired =>
        Error.Conflict(
            code: "Auth.RefreshToken.Expired",
            description: "Refresh token is invalid or has expired."
        );

    public static Error UserNotFound =>
        Error.NotFound(code: "Auth.User.NotFound", description: "User not found.");

    public static Error TokenGenerationFailed =>
        Error.Failure(
            code: "Auth.TokenGeneration.Failed",
            description: "Failed to generate new JWT token."
        );

    public static Error LaborNotFound =>
        Error.NotFound("Employee.LaborNotFound", "Labor does not exist.");
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Interfaces\IAppDbContext.cs =======

using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Common.Interfaces;

public interface IAppDbContext
{
    public DbSet<Customer> Customers { get; }
    public DbSet<Part> Parts { get; }
    public DbSet<RepairTask> RepairTasks { get; }
    public DbSet<Vehicle> Vehicles { get; }
    public DbSet<WorkOrder> WorkOrders { get; }
    public DbSet<Employee> Employees { get; }
    public DbSet<Invoice> Invoices { get; }
    public DbSet<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Interfaces\ICachedQuery.cs =======

using MechanicShop.Application.Common.Models;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Common.Interfaces;

public interface ICachedQuery
{
    public string CacheKey { get; }
    public string[] Tags { get; }
    public TimeSpan Expiration { get; }
}

public interface ICachedQuery<TResponse> : ICachedQuery, IRequest<TResponse>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Interfaces\IIdentityService.cs =======

using MechanicShop.Application.Features.Identity;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<bool> IsInRoleAsync(string userId, string role);
    Task<bool> AuthorizeAsync(string userId, string? policyName);
    Task<Result<UserInformationDto>> AuthenticateAsync(string email, string password);
    Task<Result<Guid>> CreateUserAsync(string email, string password, string role);
    Task<Result<Deleted>> DeleteUserAsync(string userId);
    Task<Result<UserInformationDto>> GetUserByIdAsync(string userId);
    Task<string?> GetUserNameAsync(string userId);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Interfaces\IInvoicePdfGenerator.cs =======

using MechanicShop.Domain.Invoices;

namespace MechanicShop.Application.Common.Interfaces;

public interface IInvoicePdfGenerator
{
    byte[] Generate(Invoice invoice);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Interfaces\ITokenProvider.cs =======

using System.Security.Claims;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Interfaces;

public interface ITokenProvider
{
    Task<Result<TokenDto>> GenerateJwtTokenAsync(
        UserInformationDto user,
        CancellationToken ct = default
    );

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Interfaces\IUser.cs =======

namespace MechanicShop.Application.Common.Interfaces;

public interface IUser
{
    public string? Id { get; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Interfaces\IWorkOrderNotifier.cs =======

namespace MechanicShop.Application.Common.Interfaces;

public interface IWorkOrderNotifier
{
    Task NotifyWorkOrdersChangedAsync(
        Guid workOrderId,
        string eventType,
        CancellationToken ct = default
    );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Interfaces\IWorkOrderPolicy.cs =======

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Common.Interfaces;

public interface IWorkOrderPolicy
{
    bool IsOutsideOperatingHours(DateTimeOffset startAt, DateTimeOffset endAt);

    Task<bool> IsLaborOccupied(
        Guid laborId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludedWorkOrderId = null
    );

    Task<bool> IsVehicleAlreadyScheduled(
        Guid vehicleId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludedWorkOrderId = null
    );

    Task<Result<Success>> CheckSpotAvailabilityAsync(
        Spot spot,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludeWorkOrderId = null,
        CancellationToken ct = default
    );

    Result<Success> ValidateMinimumRequirement(DateTimeOffset startAt, DateTimeOffset endAt);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Interfaces\UtilityService.cs =======

namespace MechanicShop.Application.Common.Interfaces
{
    public static class UtilityService
    {
        public static string MaskEmail(string email)
        {
            int atIndex = email.IndexOf('@');
            if (atIndex <= 1)
            {
                return $"****{email.AsSpan(atIndex)}";
            }

            return email[0] + "****" + email[atIndex - 1] + email[atIndex..];
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Common\Models\PaginatedList.cs =======

namespace MechanicShop.Application.Common.Models;

public sealed class PaginatedList<T>
{
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }

    public IReadOnlyCollection<T>? Items { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\AddVehicle\AddVehicleCommand.cs =======

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

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\AddVehicle\AddVehicleCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.AddVehicle;

public sealed class AddVehicleCommandHandler(
    IAppDbContext context,
    ILogger<AddVehicleCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<AddVehicleCommand, Result<VehicleDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<AddVehicleCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<VehicleDto>> Handle(AddVehicleCommand command, CancellationToken ct)
    {
        var customer = await _context
            .Customers.Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == command.CustomerId, ct);

        if (customer is null)
        {
            _logger.LogWarning(
                "Failed to add vehicle. Customer with ID {CustomerId} not found.",
                command.CustomerId
            );
            return ApplicationErrors.CustomerNotFound;
        }

        var isLicensePlateUsed = await _context.Vehicles.AnyAsync(v =>
            v.LicensePlate == command.LicensePlate
        );

        if (isLicensePlateUsed)
        {
            _logger.LogWarning(
                "Failed to add vehicle. License plate {LicensePlate} is already in use.",
                command.LicensePlate
            );

            return ApplicationErrors.VehicleAlreadyExists;
        }

        var vehicleResult = Vehicle.Create(
            Guid.NewGuid(),
            command.Make,
            command.Model,
            command.Year,
            command.LicensePlate
        );

        if (vehicleResult.IsError)
        {
            return vehicleResult.Errors;
        }

        var addVehicleResult = customer.AddVehicle(vehicleResult.Value);

        if (addVehicleResult.IsError)
        {
            return addVehicleResult.Errors;
        }

        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("customer", ct);

        _logger.LogInformation(
            "Vehicle with ID {VehicleId} was successfully added to Customer {CustomerId}.",
            vehicleResult.Value.Id,
            customer.Id
        );

        return vehicleResult.Value.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\AddVehicle\AddVehicleCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.AddVehicle;

public sealed class AddVehicleCommandValidator : AbstractValidator<AddVehicleCommand>
{
    public AddVehicleCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Customer Id Is required");

        RuleFor(x => x.Make).NotEmpty().WithMessage("Make Is required").MaximumLength(50);

        RuleFor(x => x.Model).NotEmpty().WithMessage("Model Is required").MaximumLength(50);

        RuleFor(x => x.LicensePlate)
            .NotEmpty()
            .WithMessage("License Plate Is required")
            .MaximumLength(50);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\CreateCustomer\CreateCustomerCommand.cs =======

using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public sealed record CreateCustomerCommand(
    string Name,
    string PhoneNumber,
    string Email,
    string Address,
    List<CreateVehicleDto> Vehicles
) : IRequest<Result<CustomerDto>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\CreateCustomer\CreateCustomerCommandHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandHandler(
    IAppDbContext context,
    ILogger<CreateCustomerCommandHandler> logger,
    HybridCache cache
) : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreateCustomerCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;

    public async Task<Result<CustomerDto>> Handle(
        CreateCustomerCommand command,
        CancellationToken ct
    )
    {
        var email = command.Email.Trim().ToLower();

        var exists = await _context.Customers.AnyAsync(c => c.Email!.ToLower() == email, ct);

        if (exists)
        {
            _logger.LogWarning(
                "There is an error in the customer data. The email address already exists."
            );
            return CustomerErrors.EmailAlreadyInUse;
        }

        List<Vehicle> vehicles = [];

        foreach (var v in command.Vehicles)
        {
            var vehicleResult = Vehicle.Create(
                Guid.NewGuid(),
                v.Make.Trim(),
                v.Model.Trim(),
                v.Year,
                v.LicensePlate.Trim()
            );

            if (vehicleResult.IsError)
            {
                return vehicleResult.Errors;
            }
            vehicles.Add(vehicleResult.Value);
        }

        var customerResult = Customer.Create(
            Guid.NewGuid(),
            email,
            command.Email.Trim(),
            command.PhoneNumber.Trim(),
            command.Address.Trim(),
            vehicles
        );

        if (customerResult.IsError)
        {
            return customerResult.Errors;
        }

        var customer = customerResult.Value;
        _context.Customers.Add(customer);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Customer Created Successfully with Id: {CustomerId}", customer.Id);
        await _cache.RemoveByTagAsync("customer", ct);

        return customer.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\CreateCustomer\CreateCustomerCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required").MaximumLength(100);

        RuleFor(x => x.Email).EmailAddress().WithMessage("Invalid email").MaximumLength(100);

        RuleFor(x => x.Address).NotEmpty().WithMessage("Address is required").MaximumLength(100);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Matches(@"^\+?\d{7,15}$")
            .WithMessage("Phone number must be 7â€“15 digits and may start with '+'.");

        RuleFor(x => x.Vehicles)
            .NotNull()
            .WithMessage("Vehicle list cannot be null.")
            .Must(p => p.Count > 0)
            .WithMessage("At least one vehicle is required.");

        RuleForEach(x => x.Vehicles).SetValidator(new CreateVehicleDtoValidator());
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\CreateCustomer\CreateVehicleDto.cs =======

using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public sealed record CreateVehicleDto(string Make, string Model, int Year, string LicensePlate);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\CreateCustomer\CreateVehicleDtoValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateVehicleDtoValidator : AbstractValidator<CreateVehicleDto>
{
    public CreateVehicleDtoValidator()
    {
        RuleFor(x => x.Make).NotEmpty().MaximumLength(50);

        RuleFor(x => x.Model).NotEmpty().MaximumLength(50);

        RuleFor(x => x.LicensePlate).NotEmpty().MaximumLength(10);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\RemoveCustomer\RemoveCustomerCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;

public sealed record RemoveCustomerCommand(Guid Id) : IRequest<Result<Deleted>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\RemoveCustomer\RemoveCustomerCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;

public sealed class RemoveCustomerCommandHandler(
    IAppDbContext Context,
    ILogger<RemoveCustomerCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<RemoveCustomerCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<RemoveCustomerCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Deleted>> Handle(RemoveCustomerCommand command, CancellationToken ct)
    {
        var customer = await _context.Customers.FindAsync([command.Id], ct);

        if (customer is null)
        {
            _logger.LogWarning(
                "Failed to delete customer. Customer with ID {CustomerId} was not found.",
                command.Id
            );
            return ApplicationErrors.CustomerNotFound;
        }

        var vehicleUnderWork = await _context.WorkOrders.AnyAsync(
            wo => wo.Vehicle!.CustomerId == command.Id,
            ct
        );

        if (vehicleUnderWork)
        {
            _logger.LogWarning(
                "Cannot delete customer {CustomerId}. They have associated work orders.",
                command.Id
            );
            return CustomerErrors.CannotDeleteCustomerWithWorkOrders;
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("customer", ct);

        _logger.LogInformation("Customer {CustomerId} has been deleted successfully.", command.Id);

        return Result.Deleted;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\RemoveCustomer\RemoveCustomerCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;

public sealed class RemoveCustomerCommandValidator : AbstractValidator<RemoveCustomerCommand>
{
    public RemoveCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\RemoveVehicle\RemoveVehicleCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveVehicle;

public sealed record RemoveVehicleCommand(Guid VehicleId) : IRequest<Result<Deleted>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\RemoveVehicle\RemoveVehicleCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveVehicle;

public sealed class RemoveVehicleCommandHandler(
    IAppDbContext context,
    ILogger<RemoveVehicleCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<RemoveVehicleCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RemoveVehicleCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Deleted>> Handle(RemoveVehicleCommand command, CancellationToken ct)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(
            v => v.Id == command.VehicleId,
            ct
        );

        if (vehicle is null)
        {
            _logger.LogWarning("Vehicle with id: {VehicleId} is not found", command.VehicleId);
            return ApplicationErrors.VehicleNotFound;
        }

        var vehicleUnderWork = await _context.WorkOrders.AnyAsync(
            wo => wo.VehicleId == command.VehicleId,
            ct
        );

        if (vehicleUnderWork)
        {
            _logger.LogWarning(
                "Cannot delete vehicle {VehicleId}. They have associated work orders.",
                command.VehicleId
            );
            return VehicleErrors.CannotDeleteVehicleWithWorkOrders;
        }

        _context.Vehicles.Remove(vehicle);

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("customer", ct);
        _logger.LogInformation("Vehicle {VehicleId} deleted successfully.", command.VehicleId);
        return Result.Deleted;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\RemoveVehicle\RemoveVehicleCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveVehicle;

public sealed class RemoveVehicleCommandValidator : AbstractValidator<RemoveVehicleCommand>
{
    public RemoveVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("Customer Id Is required");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\UpdateCustomer\UpdateCustomerCommand.cs =======

using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

public sealed record UpdateCustomerCommand(
    Guid Id,
    string Name,
    string Email,
    string PhoneNumber,
    string Address
) : IRequest<Result<Updated>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\UpdateCustomer\UpdateCustomerCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler(
    IAppDbContext Context,
    ILogger<UpdateCustomerCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<UpdateCustomerCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<UpdateCustomerCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Updated>> Handle(UpdateCustomerCommand command, CancellationToken ct)
    {
        var email = command.Email.ToLower().Trim();

        var exist = await _context.Customers.AnyAsync(
            c => c.Email == email && c.Id != command.Id,
            ct
        );

        if (exist)
        {
            _logger.LogWarning(
                "Customer update aborted. The email address {Email} is already in use by another customer.",
                email
            );

            return CustomerErrors.EmailAlreadyInUse;
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == command.Id, ct);

        if (customer is null)
        {
            _logger.LogWarning(
                "Failed to update customer. Customer with ID {CustomerId} was not found.",
                command.Id
            );

            return ApplicationErrors.CustomerNotFound;
        }

        var updatedCustomer = customer.Update(
            command.Name.Trim(),
            command.PhoneNumber.Trim(),
            email,
            command.Address.Trim()
        );

        if (updatedCustomer.IsError)
        {
            return updatedCustomer.Errors;
        }

        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("customer", ct);

        _logger.LogInformation("Customer {CustomerId} has been updated successfully.", command.Id);

        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\UpdateCustomer\UpdateCustomerCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required!");

        RuleFor(x => x.Name).NotEmpty().WithMessage("Id is required!").MaximumLength(100);

        RuleFor(x => x.Address).NotEmpty().WithMessage("Address is required!").MaximumLength(100);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Matches(@"^\+?\d{7,15}$")
            .WithMessage("Phone number must be 7â€“15 digits and may start with '+'.");

        RuleFor(x => x.Email).EmailAddress().WithMessage("Invalid email");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\UpdateVehicle\UpdateVehicleCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateVehicle;

public sealed record UpdateVehicleCommand(
    Guid VehicleId,
    string Make,
    string Model,
    int Year,
    string LicensePlate
) : IRequest<Result<Updated>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\UpdateVehicle\UpdateVehicleCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateVehicle;

public sealed class UpdateVehicleCommandHandler(
    IAppDbContext context,
    ILogger<UpdateVehicleCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<UpdateVehicleCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateVehicleCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Updated>> Handle(UpdateVehicleCommand command, CancellationToken ct)
    {
        var licensePlate = command.LicensePlate.Trim();
        var make = command.Make.Trim();
        var model = command.Model.Trim();

        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(
            v => v.Id == command.VehicleId,
            ct
        );

        if (vehicle is null)
        {
            _logger.LogWarning("Vehicle with id: {VehicleId} is not found", command.VehicleId);
            return ApplicationErrors.VehicleNotFound;
        }

        var isLicensePlateUsed = await _context.Vehicles.AnyAsync(
            v => v.LicensePlate == licensePlate && v.Id != command.VehicleId,
            ct
        );

        if (isLicensePlateUsed)
        {
            _logger.LogWarning(
                "Failed to update vehicle {VehicleId}. License plate {LicensePlate} is already in use by another vehicle.",
                command.VehicleId,
                licensePlate
            );
            return ApplicationErrors.VehicleAlreadyExists;
        }

        var vehicleResult = vehicle.Update(make, model, command.Year, licensePlate);

        if (vehicleResult.IsError)
        {
            return vehicleResult.Errors;
        }

        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("customer", ct);

        _logger.LogInformation("Vehicle {VehicleId} updated successfully.", command.VehicleId);

        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Commands\UpdateVehicle\UpdateVehicleCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateVehicle;

public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("Vehicle Id Is required");

        RuleFor(x => x.Make).NotEmpty().WithMessage("Make Is required").MaximumLength(50);

        RuleFor(x => x.Model).NotEmpty().WithMessage("Model Is required").MaximumLength(50);

        RuleFor(x => x.LicensePlate)
            .NotEmpty()
            .WithMessage("License Plate Is required")
            .MaximumLength(10);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\DTOs\CustomerDTO.cs =======

namespace MechanicShop.Application.Features.Customers.DTOs;

public sealed record CustomerDto(
    Guid CustomerId,
    string Name,
    string PhoneNumber,
    string Address,
    string Email,
    List<VehicleDto> Vehicles
);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\DTOs\VehicleDTO.cs =======

namespace MechanicShop.Application.Features.Customers.DTOs;

public sealed record VehicleDto(
    Guid CustomerId,
    Guid VehicleId,
    string Make,
    string Model,
    int Year,
    string LicensePlate
);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Mapper\CustomerMapper.cs =======

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

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Queries\GetAllCustomers\GetCustomersQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Features.Customers.GetAllCustomers.Queries;

public sealed record GetCustomersQuery : ICachedQuery<Result<List<CustomerDto>>>
{
    public string CacheKey => "customers";

    public string[] Tags => ["customer"];
    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Queries\GetAllCustomers\GetCustomersQueryHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Customers.GetAllCustomers.Queries;

public sealed class GetCustomersQueryHandler(IAppDbContext context)
    : IRequestHandler<GetCustomersQuery, Result<List<CustomerDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<List<CustomerDto>>> Handle(
        GetCustomersQuery request,
        CancellationToken ct
    )
    {
        var customers = await _context
            .Customers.AsNoTracking()
            .Include(c => c.Vehicles)
            .ToListAsync(ct);

        return customers.ToDtos();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Queries\GetCustomerById\GetCustomerQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid CustomerId) : ICachedQuery<Result<CustomerDto>>
{
    public string CacheKey => $"customers-{CustomerId}";

    public string[] Tags => ["customer"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Queries\GetCustomerById\GetCustomerQueryHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomerById;

public sealed class GetCustomerByIdQueryHandler(IAppDbContext context)
    : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery Query, CancellationToken ct)
    {
        var customer = await _context
            .Customers.Include(c => c.Vehicles)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == Query.CustomerId, ct);

        if (customer is null)
        {
            return ApplicationErrors.CustomerNotFound;
        }

        return customer.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Customers\Queries\GetCustomerById\GetCustomerQueryValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomerById;

public sealed class GetCustomerByIdQueryValidator : AbstractValidator<GetCustomerByIdQuery>
{
    public GetCustomerByIdQueryValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Customer id is required");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Dashboard\Dtos\DashboardStatisticsDto.cs =======

namespace MechanicShop.Application.Features.Dashboard.Dtos;

public sealed class TodayWorkOrderStatsDto
{
    public DateOnly Date { get; init; }
    public int Total { get; init; }
    public int Scheduled { get; init; }
    public int InProgress { get; init; }
    public int Completed { get; init; }
    public int Cancelled { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal TotalPartsCost { get; init; }
    public decimal TotalLaborCost { get; init; }
    public int UniqueVehicles { get; init; }
    public int UniqueCustomers { get; init; }
    public decimal NetProfit { get; init; }
    public decimal ProfitMargin { get; init; }
    public decimal CompletionRate { get; init; }
    public decimal AverageRevenuePerOrder { get; init; }
    public decimal OrdersPerVehicle { get; init; }
    public decimal PartsCostRatio { get; init; }
    public decimal LaborCostRatio { get; init; }
    public decimal CancellationRate { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Dashboard\Queries\GetWorkOrderStats\GetWorkOrderStatsQuery.cs =======

using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderStats;

public sealed record GetWorkOrderStatsQuery(DateOnly Date, TimeZoneInfo TimeZone)
    : IRequest<Result<TodayWorkOrderStatsDto>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Dashboard\Queries\GetWorkOrderStats\GetWorkOrderStatsQueryHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderStats;

public sealed class GetWorkOrderStatsQueryHandler(IAppDbContext Context)
    : IRequestHandler<GetWorkOrderStatsQuery, Result<TodayWorkOrderStatsDto>>
{
    private readonly IAppDbContext _context = Context;

    public async Task<Result<TodayWorkOrderStatsDto>> Handle(
        GetWorkOrderStatsQuery request,
        CancellationToken ct
    )
    {
        var localStart = request.Date.ToDateTime(TimeOnly.MinValue);
        var localEnd = localStart.AddDays(1);

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, request.TimeZone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, request.TimeZone);

        var query = _context
            .WorkOrders.AsNoTracking()
            .Where(wo => wo.StartAtUtc >= utcStart && wo.StartAtUtc < utcEnd);


        var totalWorkOrders = await query.CountAsync(ct);

        if (totalWorkOrders == 0)
        {
            return new TodayWorkOrderStatsDto
            {
                Date = request.Date,
                Total = 0,
                Scheduled = 0,
                InProgress = 0,
                Completed = 0,
                Cancelled = 0,
                TotalRevenue = 0,
                TotalPartsCost = 0,
                TotalLaborCost = 0,
                UniqueVehicles = 0,
                UniqueCustomers = 0,
                NetProfit = 0,
                ProfitMargin = 0,
                CompletionRate = 0,
                AverageRevenuePerOrder = 0,
                OrdersPerVehicle = 0,
                PartsCostRatio = 0,
                LaborCostRatio = 0,
                CancellationRate = 0,
            };
        }

        var stats = await query.GroupBy(x => 1)
        .Select(g => new
        {
            Scheduled = g.Count(wo => wo.State == WorkOrderState.Scheduled),
            InProgress = g.Count(wo => wo.State == WorkOrderState.InProgress),
            Completed = g.Count(wo => wo.State == WorkOrderState.Completed),
            Cancelled = g.Count(wo => wo.State == WorkOrderState.Cancelled),
        }).FirstOrDefaultAsync(ct);

       var financialTotals = await query
       .Where(wo => wo.Invoice != null)
       .Select(wo => new
       {
           LaborCost = wo.Invoice!.ActualLaborCost,
           PartsCost = wo.Invoice.ActualPartsCost,
           DiscountAmount = wo.Invoice!.DiscountAmount ?? 0m,
           TaxRate = wo.Invoice!.TaxRate,
           GrossSubtotal = wo.Invoice!.LineItems.Sum(li => li.Quantity * li.UnitPrice)
       })
       .GroupBy(wo => 1)
       .Select(g => new
       {
           TotalRevenue = g.Sum(x => x.GrossSubtotal * (1 + x.TaxRate)),
           TotalPartsCost = g.Sum(x => x.PartsCost),
           TotalLaborCost = g.Sum(x => x.LaborCost),
           TotalTax = g.Sum(x => (x.GrossSubtotal - x.DiscountAmount) * x.TaxRate),
       })
       .FirstOrDefaultAsync(ct);

        var totalRevenue = financialTotals?.TotalRevenue ?? 0;
        var totalPartCost = financialTotals?.TotalPartsCost ?? 0;
        var totalLaborCost = financialTotals?.TotalLaborCost ?? 0;
        var totalTax = financialTotals?.TotalTax ?? 0;

        var netProfit = totalRevenue - totalPartCost - totalLaborCost - totalTax;

        var uniqueVehicles = await query.Select(s => s.VehicleId).Distinct().CountAsync(ct);
        var uniqueCustomers = await query
            .Select(s => s.Vehicle!.CustomerId)
            .Distinct()
            .CountAsync(ct);

        var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;
        var laborCostRatio = totalRevenue > 0 ? (totalLaborCost / totalRevenue) * 100 : 0;
        var partsCostRatio = totalRevenue > 0 ? (totalPartCost / totalRevenue) * 100 : 0;

        var completionRate =
            totalWorkOrders > 0 ? ((decimal)stats!.Completed / totalWorkOrders) * 100 : 0;
        var cancellationRate =
            totalWorkOrders > 0 ? ((decimal)stats!.Cancelled / totalWorkOrders) * 100 : 0;
        var averageRevenuePerOrder = totalWorkOrders > 0 ? (totalRevenue / totalWorkOrders) : 0;
        var ordersPerVehicle = uniqueVehicles > 0 ? ((decimal)totalWorkOrders / uniqueVehicles) : 0;

        return new TodayWorkOrderStatsDto
        {
            Date = request.Date,
            Total = totalWorkOrders,
            Scheduled = stats!.Scheduled,
            InProgress = stats.InProgress,
            Completed = stats.Completed,
            Cancelled = stats.Cancelled,
            TotalRevenue = totalRevenue,
            TotalPartsCost = totalPartCost,
            TotalLaborCost = totalLaborCost,
            UniqueVehicles = uniqueVehicles,
            UniqueCustomers = uniqueCustomers,
            NetProfit = netProfit,
            ProfitMargin = profitMargin,
            CompletionRate = completionRate,
            AverageRevenuePerOrder = averageRevenuePerOrder,
            OrdersPerVehicle = ordersPerVehicle,
            PartsCostRatio = partsCostRatio,
            LaborCostRatio = laborCostRatio,
            CancellationRate = cancellationRate,
        };
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Dashboard\Queries\GetWorkOrderStats\GetWorkOrderStatsQueryValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderStats
{
    public class GetWorkOrderStatsQueryValidator : AbstractValidator<GetWorkOrderStatsQuery>
    {
        public GetWorkOrderStatsQueryValidator()
        {
            RuleFor(request => request.Date).NotEmpty().WithMessage("Date is required.");
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Employees\Commands\CreateEmployeeCommand.cs =======

using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;

namespace MechanicShop.Application.Features.Employees.Commands;

public sealed record CreateEmployeeCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Role Role
) : IRequest<Result<EmployeeDto>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Employees\Commands\CreateEmployeeCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.Employees.Mapper;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Employees.Commands;

public sealed class CreateEmployeeCommandHandler(
    IAppDbContext context,
    ILogger<CreateEmployeeCommandHandler> logger,
    HybridCache cache,
    IIdentityService identityService
) : IRequestHandler<CreateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreateEmployeeCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;
    private readonly IIdentityService _identityService = identityService;

    public async Task<Result<EmployeeDto>> Handle(
        CreateEmployeeCommand command,
        CancellationToken ct
    )
    {
        var identityResult = await _identityService.CreateUserAsync(
            command.Email,
            command.Password,
            command.Role.ToString()
        );

        if (identityResult.IsError)
        {
            _logger.LogWarning(
                "Failed to create identity for employee with email: {Email}.",
                command.Email
            );
            return identityResult.Errors;
        }

        var userId = identityResult.Value;

        var employeeResult = Employee.Create(
            userId,
            command.FirstName,
            command.LastName,
            command.Role
        );

        if (employeeResult.IsError)
        {
            await _identityService.DeleteUserAsync(userId.ToString());
            return employeeResult.Errors;
        }

        var employee = employeeResult.Value;

        try
        {
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Database save failed for Employee {EmployeeId}. Deleting Identity account.",
                employee.Id
            );
            await _identityService.DeleteUserAsync(userId.ToString());
            throw;
        }

        _logger.LogInformation("Employee with id: {EmployeeId} added successfully", employee.Id);

        await _cache.RemoveByTagAsync("employee", ct);

        return employee.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Employees\Commands\CreateEmployeeCommandValidator.cs =======

using System.Data;
using FluentValidation;

namespace MechanicShop.Application.Features.Employees.Commands;

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(e => e.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MaximumLength(50);

        RuleFor(e => e.LastName).NotEmpty().WithMessage("Last name is required").MaximumLength(50);

        RuleFor(e => e.Role)
            .IsInEnum()
            .WithMessage("Role name is required and must be a valid role.");

        RuleFor(e => e.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format");

        RuleFor(e => e.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(6)
            .WithMessage("Password must be at least 6 characters");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Employees\DTOs\EmployeeDto.cs =======

namespace MechanicShop.Application.Features.Employees.DTOs;

public sealed class EmployeeDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Employees\Mapper\EmployeeMapper.cs =======

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

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Employees\Queries\GetEmployeeById\GetEmployeesQueryById.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployees;

public sealed record GetEmployeeByIdQuery(Guid Id) : ICachedQuery<Result<EmployeeDto>>
{
    public string CacheKey => $"employee-{Id}";

    public string[] Tags => ["employee"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Employees\Queries\GetEmployeeById\GetEmployeesQueryByIdHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.Employees.Mapper;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployees;

public sealed class GetEmployeesQueryByIdHandler(
    IAppDbContext context,
    ILogger<GetEmployeesQueryByIdHandler> logger
) : IRequestHandler<GetEmployeeByIdQuery, Result<EmployeeDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<GetEmployeesQueryByIdHandler> _logger = logger;

    public async Task<Result<EmployeeDto>> Handle(
        GetEmployeeByIdQuery request,
        CancellationToken ct
    )
    {
        var employee = await _context
            .Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);

        if (employee is null)
        {
            _logger.LogWarning("Employee with id: {Id} not found", request.Id);
            return ApplicationErrors.EmployeeNotFound;
        }
        return employee.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Employees\Queries\GetEmployees\GetEmployeesQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployees;

public sealed record GetEmployeesQuery(Role? Role) : ICachedQuery<Result<List<EmployeeDto>>>
{
    public string CacheKey => $"employees-{Role?.ToString() ?? "all"}";

    public string[] Tags => ["employee"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Employees\Queries\GetEmployees\GetEmployeesQueryHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.Employees.Mapper;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployees;

public sealed class GetEmployeesQueryHandler(
    IAppDbContext context,
    ILogger<GetEmployeesQueryHandler> logger
) : IRequestHandler<GetEmployeesQuery, Result<List<EmployeeDto>>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<GetEmployeesQueryHandler> _logger = logger;

    public async Task<Result<List<EmployeeDto>>> Handle(
        GetEmployeesQuery request,
        CancellationToken ct
    )
    {
        var query = _context.Employees.AsNoTracking().AsQueryable();

        if (request.Role.HasValue)
        {
            query = query.Where(e => e.Role == request.Role.Value);
        }
        var employees = await query.ToListAsync(ct);

        return employees.ToDtos();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Identity\UserInformation.cs =======

using System.Security.Claims;

namespace MechanicShop.Application.Features.Identity;

public sealed record UserInformationDto(
    string UserId,
    string Email,
    IList<string> Roles,
    IList<Claim> Claims
);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Identity\DTOs\TokenDto.cs =======

namespace MechanicShop.Application.Features.Identity;

public sealed class TokenDto
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Identity\Queries\GenerateToken\GenerateTokenQuery.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Identity.Queries.GenerateTokens;

public sealed record GenerateTokenQuery(string Email, string Password) : IRequest<Result<TokenDto>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Identity\Queries\GenerateToken\GenerateTokenQueryHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.GenerateTokens;

public class GenerateTokenQueryHandler(
    IAppDbContext Context,
    ILogger<GenerateTokenQueryHandler> logger,
    IIdentityService identityService,
    ITokenProvider tokenProvider
) : IRequestHandler<GenerateTokenQuery, Result<TokenDto>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<GenerateTokenQueryHandler> _logger = logger;
    private readonly IIdentityService _identityService = identityService;
    private readonly ITokenProvider _tokenProvider = tokenProvider;

    public async Task<Result<TokenDto>> Handle(GenerateTokenQuery query, CancellationToken ct)
    {
        var userResponse = await _identityService.AuthenticateAsync(query.Email, query.Password);

        if (userResponse.IsError)
        {
            return userResponse.Errors;
        }

        var generateTokenResult = await _tokenProvider.GenerateJwtTokenAsync(
            userResponse.Value,
            ct
        );

        if (generateTokenResult.IsError)
        {
            _logger.LogError(
                "Generate token error occurred: {ErrorDescription}",
                generateTokenResult.TopError.Description
            );

            return generateTokenResult.Errors;
        }

        await _context
            .RefreshTokens.Where(rt => rt.UserId == userResponse.Value.UserId)
            .ExecuteDeleteAsync(ct);

        var refreshTokenResult = RefreshToken.Create(
            Guid.NewGuid(),
            generateTokenResult.Value.RefreshToken,
            userResponse.Value.UserId.ToString(),
            DateTimeOffset.UtcNow.AddDays(7)
        );
        if (refreshTokenResult.IsError)
        {
            _logger.LogError("Failed to create RefreshToken entity.");
            return refreshTokenResult.Errors;
        }
        _context.RefreshTokens.Add(refreshTokenResult.Value);
        await _context.SaveChangesAsync(ct);

        return generateTokenResult.Value;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Identity\Queries\GenerateToken\GenerateTokenQueryValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Identity.Queries.GenerateTokens;

public sealed class GenerateTokenQueryValidator : AbstractValidator<GenerateTokenQuery>
{
    public GenerateTokenQueryValidator()
    {
        RuleFor(request => request.Email)
            .NotNull()
            .NotEmpty()
            .WithErrorCode("Email_Null_Or_Empty")
            .WithMessage("Email cannot be null or empty")
            .EmailAddress()
            .WithMessage("the email address is wrong");

        RuleFor(request => request.Password)
            .NotNull()
            .NotEmpty()
            .WithErrorCode("Password_Null_Or_Empty")
            .WithMessage("Password cannot be null or empty.");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Identity\Queries\GetUserInfo\GetUserByIdQuery.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Identity.Queries.GetUserInfo;

public sealed record GetUserByIdQuery(string? UserId) : IRequest<Result<UserInformationDto>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Identity\Queries\GetUserInfo\GetUserByIdQueryHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.GetUserInfo;

public sealed class GetUserByIdQueryHandler(
    ILogger<GetUserByIdQueryHandler> logger,
    IIdentityService identityService
) : IRequestHandler<GetUserByIdQuery, Result<UserInformationDto>>
{
    private readonly ILogger<GetUserByIdQueryHandler> _logger = logger;
    private readonly IIdentityService _identityService = identityService;

    public async Task<Result<UserInformationDto>> Handle(
        GetUserByIdQuery request,
        CancellationToken ct
    )
    {
        var getUserByIdResult = await _identityService.GetUserByIdAsync(request.UserId!);

        if (getUserByIdResult.IsError)
        {
            _logger.LogError(
                "User with Id { UserId }{ErrorDetails}",
                request.UserId,
                getUserByIdResult.TopError.Description
            );

            return getUserByIdResult.Errors;
        }

        return getUserByIdResult.Value;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Identity\Queries\RefreshTokens\RefreshTokenQuery.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Identity.Queries.RefreshTokens;

public record RefreshTokenQuery(string RefreshToken, string ExpiredAccessToken)
    : IRequest<Result<TokenDto>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Identity\Queries\RefreshTokens\RefreshTokenQueryHandler.cs =======

using System.Security.Claims;
using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.RefreshTokens;

public class RefreshTokenQueryHandler(
    ILogger<RefreshTokenQueryHandler> logger,
    IIdentityService identityService,
    IAppDbContext context,
    ITokenProvider tokenProvider,
    TimeProvider timeProvider
) : IRequestHandler<RefreshTokenQuery, Result<TokenDto>>
{
    private readonly ILogger<RefreshTokenQueryHandler> _logger = logger;
    private readonly IIdentityService _identityService = identityService;
    private readonly IAppDbContext _context = context;
    private readonly ITokenProvider _tokenProvider = tokenProvider;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Result<TokenDto>> Handle(RefreshTokenQuery query, CancellationToken ct)
    {
        var principal = _tokenProvider.GetPrincipalFromExpiredToken(query.ExpiredAccessToken);

        if (principal is null)
        {
            _logger.LogError("Expired access token is not valid");
            return ApplicationErrors.ExpiredAccessTokenInvalid;
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        if (userId is null)
        {
            _logger.LogError("Invalid userId claim");

            return ApplicationErrors.UserIdClaimInvalid;
        }

        var getUserResult = await _identityService.GetUserByIdAsync(userId);
        if (getUserResult.IsError)
        {
            return getUserResult.Errors;
        }

        var oldRefreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(
            r => r.Token == query.RefreshToken && r.UserId == userId,
            ct
        );

        if (oldRefreshToken is null || oldRefreshToken.ExpiresOnUtc < _timeProvider.GetUtcNow())
        {
            return ApplicationErrors.RefreshTokenExpired;
        }
        var generateTokenResult = await _tokenProvider.GenerateJwtTokenAsync(
            getUserResult.Value,
            ct
        );
        if (generateTokenResult.IsError)
        {
            return generateTokenResult.Errors;
        }

        _context.RefreshTokens.Remove(oldRefreshToken);

        var newRefreshTokenResult = RefreshToken.Create(
            Guid.NewGuid(),
            generateTokenResult.Value.RefreshToken,
            userId,
            _timeProvider.GetUtcNow().AddDays(7)
        );
        if (newRefreshTokenResult.IsError)
        {
            return newRefreshTokenResult.Errors;
        }
        _context.RefreshTokens.Add(newRefreshTokenResult.Value);
        await _context.SaveChangesAsync(ct);
        return generateTokenResult.Value;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Commands\IssueInvoice\IssueInvoiceCommand.cs =======

using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Commands.IssueInvoice;

public sealed record IssueInvoiceCommand(Guid WorkOrderId, decimal? Discount)
    : IRequest<Result<InvoiceDto>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Commands\IssueInvoice\IssueInvoiceCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Application.Features.Invoices.Mapper;
using MechanicShop.Domain.Common.Constraints;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Commands.IssueInvoice;

public sealed class IssueInvoiceCommandHandler(
    IAppDbContext Context,
    ILogger<IssueInvoiceCommandHandler> Logger,
    TimeProvider Datetime,
    HybridCache Cache
) : IRequestHandler<IssueInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<IssueInvoiceCommandHandler> _logger = Logger;
    private readonly TimeProvider _datetime = Datetime;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<InvoiceDto>> Handle(IssueInvoiceCommand command, CancellationToken ct)
    {
        var workOrder = await _context
            .WorkOrders.Include(wo => wo.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(wo => wo.Invoice)
            .Include(wo => wo.RepairTasks)
                .ThenInclude(wo => wo.Parts)
            .FirstOrDefaultAsync(wo => wo.Id == command.WorkOrderId, ct);

        if (workOrder is null)
        {
            _logger.LogWarning("WorkOrder with id: {WorkOrderId} no found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }
        if (workOrder.State != WorkOrderState.Completed)
        {
            _logger.LogWarning(
                "WorkOrder with id: {WorkOrderId} is not completed",
                command.WorkOrderId
            );
            return ApplicationErrors.WorkOrderMustBeCompletedForInvoicing;
        }

        var invoiceId = Guid.NewGuid();
        var lineItems = new List<InvoiceLineItem>();
        var lineNumber = 1;

        foreach (var (task, taskIndex) in workOrder.RepairTasks.Select((t, i) => (t, i + 1)))
        {
            var partsSummary = task.Parts.Any()
                ? string.Join(
                    Environment.NewLine,
                    task.Parts.Select(p => $"â€¢ {p.Name} x {p.Quantity} @ {p.Cost:C}")
                )
                : "No parts";

            var lineDescription =
                $"{taskIndex}: {task.Name}{Environment.NewLine}"
                + $"  Labor = {task.LaborCost:C}{Environment.NewLine}"
                + $"  Parts:{Environment.NewLine}{partsSummary}";

            var totalPartsCost = task.Parts.Sum(p => p.Quantity * p.Cost);

            var totalTaskCost = task.LaborCost + totalPartsCost;

            var lineItemResult = InvoiceLineItem.Create(
                invoiceId,
                lineNumber++,
                lineDescription,
                1,
                totalTaskCost
            );

            if (lineItemResult.IsError)
            {
                return lineItemResult.Errors;
            }

            lineItems.Add(lineItemResult.Value);
        }

        var discountAmount = command.Discount ?? 0m;

        var createInvoiceResult = Invoice.Create(
            id: invoiceId,
            workOrderId: workOrder.Id,
            items: lineItems,
            discountAmount: discountAmount,
            taxRate: MechanicShopConstraints.TaxRate,
            datetime: _datetime,
            laborCost: workOrder.RepairTasks.Sum(rt => rt.LaborCost),
            partsCost: workOrder
                .RepairTasks.SelectMany(rt => rt.Parts)
                .Sum(p => p.Cost * p.Quantity)
        );
        if (createInvoiceResult.IsError)
        {
            _logger.LogWarning(
                "Invoice creation failed for WorkOrderId: {WorkOrderId}. Errors: {@Errors}",
                command.WorkOrderId,
                createInvoiceResult.Errors
            );
            return createInvoiceResult.Errors;
        }

        var invoice = createInvoiceResult.Value;

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("invoice", ct);
        _logger.LogInformation("invoice with id: {InvoiceId} added successfully", invoiceId);

        return invoice.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Commands\IssueInvoice\IssueInvoiceCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Invoices.Commands.IssueInvoice;

public sealed class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
{
    public IssueInvoiceCommandValidator()
    {
        RuleFor(i => i.WorkOrderId).NotEmpty().WithMessage("Work order id is required");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Commands\SettleInvoice\SettleInvoiceCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Commands.SettleInvoice;

public sealed record SettleInvoiceCommand(Guid InvoiceId) : IRequest<Result<Success>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Commands\SettleInvoice\SettleInvoiceCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Commands.SettleInvoice;

public sealed class SettleInvoiceCommandHandler(
    IAppDbContext Context,
    ILogger<SettleInvoiceCommandHandler> Logger,
    TimeProvider DateTime,
    HybridCache Cache
) : IRequestHandler<SettleInvoiceCommand, Result<Success>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<SettleInvoiceCommandHandler> _logger = Logger;
    private readonly TimeProvider _dateTime = DateTime;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Success>> Handle(SettleInvoiceCommand command, CancellationToken ct)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(
            i => i.Id == command.InvoiceId,
            ct
        );

        if (invoice is null)
        {
            _logger.LogWarning("Invoice with id: {InvoiceId} no found", command.InvoiceId);
            return ApplicationErrors.InvoiceNotFound;
        }
        var payInvoiceResult = invoice.MarkAsPaid(_dateTime);

        if (payInvoiceResult.IsError)
        {
            _logger.LogWarning(
                "Invoice payment failed for InvoiceId: {InvoiceId}. Errors: {Errors}",
                invoice.Id,
                payInvoiceResult.Errors
            );
            return payInvoiceResult.Errors;
        }
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("invoice", ct);
        _logger.LogInformation("Invoice {InvoiceId} successfully paid.", invoice.Id);
        return Result.Success;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Commands\SettleInvoice\SettleInvoiceCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Invoices.Commands.SettleInvoice;

public sealed class SettleInvoiceCommandValidator : AbstractValidator<SettleInvoiceCommand>
{
    public SettleInvoiceCommandValidator()
    {
        RuleFor(i => i.InvoiceId).NotEmpty().WithMessage("Invoice id is required");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\DTOs\InvoiceDto.cs =======

using MechanicShop.Application.Features.Customers.DTOs;

namespace MechanicShop.Application.Features.Invoices.DTOs;

public sealed record InvoiceDto(
    Guid InvoiceId,
    Guid WorkOrderId,
    DateTimeOffset IssuedAtUtc,
    CustomerDto? Customer,
    VehicleDto? Vehicle,
    decimal? DiscountAmount,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total,
    string? PaymentStatus,
    List<InvoiceLineItemDto> Items
);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\DTOs\InvoiceLineItemDto.cs =======

namespace MechanicShop.Application.Features.Invoices.DTOs;

public sealed record InvoiceLineItemDto(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\DTOs\InvoicePdfDto.cs =======

namespace MechanicShop.Application.Features.Invoices.DTOs;

public sealed class InvoicePdfDto
{
    public byte[]? Content { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; } = "application/pdf";
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Mapper\InvoiceMapper.cs =======

using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

namespace MechanicShop.Application.Features.Invoices.Mapper;

public static class InvoiceMapper
{
    public static InvoiceDto ToDto(this Invoice entity)
    {
        return new InvoiceDto(
            entity.Id,
            entity.WorkOrderId,
            entity.IssuedAtUtc,
            entity.WorkOrder!.Vehicle!.Customer!.ToDto(),
            entity.WorkOrder.Vehicle.ToDto(),
            entity.DiscountAmount,
            entity.Subtotal,
            entity.TaxRate,
            entity.Total,
            entity.Status.ToString(),
            entity.LineItems.ToDtos()
        );
    }

    public static List<InvoiceDto> ToDtos(this IEnumerable<Invoice> entities)
    {
        return [.. entities.Select(e => e.ToDto())];
    }

    public static InvoiceLineItemDto ToDto(this InvoiceLineItem entity)
    {
        return new InvoiceLineItemDto(
            entity.Description,
            entity.Quantity,
            entity.UnitPrice,
            entity.LineTotal
        );
    }

    public static List<InvoiceLineItemDto> ToDtos(this IEnumerable<InvoiceLineItem> entities)
    {
        return [.. entities.Select(e => e.ToDto())];
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Queries\GetInvoiceById\GetInvoiceByIdQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(Guid InvoiceId) : ICachedQuery<Result<InvoiceDto>>
{
    public string CacheKey => $"Invoice-{InvoiceId}";

    public string[] Tags => ["invoices"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Queries\GetInvoiceById\GetInvoiceByIdQueryHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Application.Features.Invoices.Mapper;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdQueryHandler(
    IAppDbContext context,
    ILogger<GetInvoiceByIdQueryHandler> Logger
) : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IAppDbContext _context = context;

    private readonly ILogger<GetInvoiceByIdQueryHandler> _logger = Logger;

    public async Task<Result<InvoiceDto>> Handle(GetInvoiceByIdQuery query, CancellationToken ct)
    {
        var invoice = await _context
            .Invoices.AsNoTracking() //
            .Include(i => i.LineItems) //
            .Include(i => i.WorkOrder) //
                .ThenInclude(wo => wo!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .FirstOrDefaultAsync(i => i.Id == query.InvoiceId, ct);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice with id: {InvoiceId} no found", query.InvoiceId);
            return ApplicationErrors.InvoiceNotFound;
        }

        return invoice.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Queries\GetInvoiceById\GetInvoiceByIdQueryValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdQueryValidator : AbstractValidator<GetInvoiceByIdQuery>
{
    public GetInvoiceByIdQueryValidator()
    {
        RuleFor(i => i.InvoiceId).NotEmpty().WithMessage("Invoice id is required");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Queries\GetInvoicePdf\GetInvoicePdfQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoicePdf;

public sealed record GetInvoicePdfQuery(Guid InvoiceId) : ICachedQuery<Result<InvoicePdfDto>>
{
    public string CacheKey => $"InvoicePdf-{InvoiceId}";

    public string[] Tags => ["invoices"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Queries\GetInvoicePdf\GetInvoicePdfQueryHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Application.Features.Invoices.Mapper;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoicePdf;

public sealed class GetInvoicePdfQueryHandler(
    IAppDbContext context,
    IInvoicePdfGenerator pdfGenerator,
    ILogger<GetInvoicePdfQueryHandler> Logger
) : IRequestHandler<GetInvoicePdfQuery, Result<InvoicePdfDto>>
{
    private readonly IAppDbContext _context = context;

    private readonly ILogger<GetInvoicePdfQueryHandler> _logger = Logger;

    public async Task<Result<InvoicePdfDto>> Handle(GetInvoicePdfQuery query, CancellationToken ct)
    {
        var invoice = await _context
            .Invoices.AsNoTracking()
            .Include(i => i.LineItems)
            .Include(i => i.WorkOrder)
                .ThenInclude(wo => wo!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .FirstOrDefaultAsync(i => i.Id == query.InvoiceId, ct);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice with id: {InvoiceId} no found", query.InvoiceId);
            return ApplicationErrors.InvoiceNotFound;
        }

        try
        {
            var pdfBytes = await Task.Run(() => pdfGenerator.Generate(invoice), ct);
            var invoicePdf = new InvoicePdfDto
            {
                Content = pdfBytes,
                FileName = $"Invoice-{invoice.Id}.pdf",
            };
            return invoicePdf;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to generate PDF for InvoiceId: {InvoiceId}",
                query.InvoiceId
            );
            return Error.Failure("An error occurred while generating the invoice PDF.");
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Invoices\Queries\GetInvoicePdf\GetInvoicePdfQueryValidator.cs =======

using FluentValidation;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoicePdf;

public sealed class GetInvoicePdfQueryValidator : AbstractValidator<GetInvoicePdfQuery>
{
    public GetInvoicePdfQueryValidator()
    {
        RuleFor(i => i.InvoiceId).NotEmpty().WithMessage("Invoice id is required");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\CreateRepairTask\CreatePartsDto.cs =======

namespace MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;

public sealed record CreatePartsDto(string Name, decimal Cost, int Quantity);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\CreateRepairTask\CreatePartsDtoValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;

public class CreatePartsDtoValidator : AbstractValidator<CreatePartsDto>
{
    public CreatePartsDtoValidator()
    {
        RuleFor(p => p.Name).NotEmpty().WithMessage("Name is required").MaximumLength(50);

        RuleFor(p => p.Cost).GreaterThan(0).WithMessage("Cost must be greater than 0");

        RuleFor(p => p.Quantity)
            .InclusiveBetween(1, 10)
            .WithMessage("Quantity must be between 1 and 10");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\CreateRepairTask\CreateRepairTaskCommand.cs =======

using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Enums;
using MediatR;

namespace MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;

public sealed record CreateRepairTaskCommand(
    string Name,
    RepairDurationInMinutes EstimatedDurationInMins,
    decimal LaborCost,
    List<CreatePartsDto> Parts
) : IRequest<Result<RepairTaskDto>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\CreateRepairTask\CreateRepairTaskCommandHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;

public sealed class CreateRepairTaskCommandHandler(
    IAppDbContext Context,
    ILogger<CreateRepairTaskCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<CreateRepairTaskCommand, Result<RepairTaskDto>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<CreateRepairTaskCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<RepairTaskDto>> Handle(
        CreateRepairTaskCommand command,
        CancellationToken ct
    )
    {
        var repairTaskExist = await _context.RepairTasks.AnyAsync(
            c => c.Name.ToLower() == command.Name.ToLower(),
            ct
        );
        if (repairTaskExist)
        {
            _logger.LogWarning(
                "Repair task with name: {RepairTaskName} is already exists",
                command.Name
            );
            return RepairTaskErrors.NameAlreadyExists;
        }

        var parts = new List<Part>();

        foreach (var p in command.Parts)
        {
            var part = Part.Create(Guid.NewGuid(), p.Name, p.Cost, p.Quantity);
            if (part.IsError)
            {
                return part.Errors;
            }
            parts.Add(part.Value);
        }

        var duplicateGroup = command
            .Parts.GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateGroup is not null)
        {
            var duplicatePartName = duplicateGroup.First().Name;

            _logger.LogWarning(
                "There is a duplicate in parts: {DuplicatePartName}",
                duplicatePartName
            );

            return RepairTaskErrors.DuplicateName;
        }
        var repairTaskResult = RepairTask.Create(
            Guid.NewGuid(),
            command.Name,
            command.EstimatedDurationInMins,
            command.LaborCost,
            parts
        );

        if (repairTaskResult.IsError)
        {
            return repairTaskResult.Errors;
        }

        var repairTask = repairTaskResult.Value;

        _context.RepairTasks.Add(repairTask);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Repair task with id {RepairTaskId} added successfully",
            repairTask.Id
        );

        await _cache.RemoveByTagAsync("repair-task", ct);

        return repairTask.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\CreateRepairTask\CreateRepairTaskCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;

public sealed class CreateRepairTaskCommandValidator : AbstractValidator<CreateRepairTaskCommand>
{
    public CreateRepairTaskCommandValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithMessage("Name is required").MaximumLength(50);

        RuleFor(r => r.EstimatedDurationInMins)
            .NotEmpty()
            .WithMessage("Duration time is required")
            .IsInEnum();

        RuleFor(r => r.LaborCost).GreaterThan(0).WithMessage("Cost must be greater than 0");

        RuleFor(r => r.Parts).NotNull().WithMessage("Parts is required");

        RuleForEach(r => r.Parts).SetValidator(new CreatePartsDtoValidator());
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\RemoveRepairTask\RemoveRepairTaskCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.RepairTasks.Command.RemoveRepairTask;

public sealed record RemoveRepairTaskCommand(Guid RepairTaskId) : IRequest<Result<Deleted>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\RemoveRepairTask\RemoveRepairTaskCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Command.RemoveRepairTask;

public sealed class RemoveRepairTaskCommandHandler(
    IAppDbContext Context,
    ILogger<RemoveRepairTaskCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<RemoveRepairTaskCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<RemoveRepairTaskCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Deleted>> Handle(RemoveRepairTaskCommand command, CancellationToken ct)
    {
        var repairTask = await _context.RepairTasks.FindAsync([command.RepairTaskId], ct);

        if (repairTask is null)
        {
            _logger.LogWarning(
                "RepairTask {RepairTaskId} not found for deletion.",
                command.RepairTaskId
            );
            return ApplicationErrors.RepairTaskNotFound;
        }

        var isInUse = await _context
            .WorkOrders.AsNoTracking()
            .SelectMany(wo => wo.RepairTasks)
            .AnyAsync(rt => rt.Id == command.RepairTaskId, ct);

        if (isInUse)
        {
            _logger.LogWarning(
                "RepairTask {RepairTaskId} cannot be deleted â€” in use by work orders.",
                command.RepairTaskId
            );

            return RepairTaskErrors.InUse;
        }

        _context.RepairTasks.Remove(repairTask);
        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("repair-task", ct);

        _logger.LogInformation(
            "RepairTask {RepairTaskId} deleted successfully.",
            command.RepairTaskId
        );

        return Result.Deleted;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\RemoveRepairTask\RemoveRepairTaskCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Command.RemoveRepairTask;

public sealed class RemoveRepairTaskCommandValidator : AbstractValidator<RemoveRepairTaskCommand>
{
    public RemoveRepairTaskCommandValidator()
    {
        RuleFor(r => r.RepairTaskId).NotEmpty().WithMessage("Id is required");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\UpdateRepairTask\UpdatePartsDto.cs =======

namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed record UpdatePartsDto(Guid? PartId, string Name, decimal Cost, int Quantity);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\UpdateRepairTask\UpdatePartsDtoValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed class UpdatePartsDtoValidator : AbstractValidator<UpdatePartsDto>
{
    public UpdatePartsDtoValidator()
    {
        RuleFor(p => p.PartId).NotEmpty().WithMessage("Id is required");

        RuleFor(p => p.Name).NotEmpty().WithMessage("Name is required").MaximumLength(50);

        RuleFor(p => p.Cost).GreaterThan(0).WithMessage("Cost must be greater than 0");

        RuleFor(p => p.Quantity)
            .InclusiveBetween(1, 10)
            .WithMessage("Quantity must be between 1 and 10");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\UpdateRepairTask\UpdateRepairTaskCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Enums;
using MediatR;

namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed record UpdateRepairTaskCommand(
    Guid RepairTaskId,
    string Name,
    RepairDurationInMinutes EstimatedDurationInMins,
    decimal LaborCost,
    List<UpdatePartsDto> Parts
) : IRequest<Result<Updated>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\UpdateRepairTask\UpdateRepairTaskCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed class UpdateRepairTaskCommandHandler(
    IAppDbContext Context,
    ILogger<UpdateRepairTaskCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<UpdateRepairTaskCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<UpdateRepairTaskCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Updated>> Handle(UpdateRepairTaskCommand command, CancellationToken ct)
    {
        var nameExists = await _context.RepairTasks.AnyAsync(
            rt => rt.Name.ToLower() == command.Name.ToLower() && rt.Id != command.RepairTaskId,
            ct
        );

        if (nameExists)
        {
            _logger.LogWarning("Repair task with name: {Name} already exists", command.Name);
            return RepairTaskErrors.NameAlreadyExists;
        }

        var repairTask = await _context
            .RepairTasks.Include(R => R.Parts)
            .FirstOrDefaultAsync(r => r.Id == command.RepairTaskId, ct);

        if (repairTask is null)
        {
            _logger.LogWarning(
                "Repair task with id: {RepairTaskId} not found",
                command.RepairTaskId
            );
            return ApplicationErrors.RepairTaskNotFound;
        }

        var validatedParts = new List<Part>();
        foreach (var p in command.Parts)
        {
            var partId = p.PartId ?? Guid.NewGuid();

            var partResult = Part.Create(partId, p.Name, p.Cost, p.Quantity);

            if (partResult.IsError)
            {
                return partResult.Errors;
            }

            validatedParts.Add(partResult.Value);
        }

        var resultUpdated = repairTask.Update(
            command.Name,
            command.EstimatedDurationInMins,
            command.LaborCost
        );

        if (resultUpdated.IsError)
        {
            return resultUpdated.Errors;
        }

        var upsertPartsResult = repairTask.UpsertParts(validatedParts);

        if (upsertPartsResult.IsError)
        {
            return upsertPartsResult.Errors;
        }

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("repair-task", ct);

        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Command\UpdateRepairTask\UpdateRepairTaskCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed class UpdateRepairTaskCommandValidator : AbstractValidator<UpdateRepairTaskCommand>
{
    public UpdateRepairTaskCommandValidator()
    {
        RuleFor(p => p.RepairTaskId).NotEmpty().WithMessage("Id is required");

        RuleFor(r => r.Name).NotEmpty().WithMessage("Name is required").MaximumLength(50);

        RuleFor(r => r.EstimatedDurationInMins)
            .NotEmpty()
            .WithMessage("Duration time is required")
            .IsInEnum();

        RuleFor(r => r.LaborCost).GreaterThan(0).WithMessage("Cost must be greater than 0");

        RuleForEach(r => r.Parts).SetValidator(new UpdatePartsDtoValidator());
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\DTOs\PartsDto.cs =======

namespace MechanicShop.Application.Features.RepairTasks.DTOs;

public sealed record PartsDto(string Name, decimal Cost, int Quantity);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\DTOs\RepairTaskDto.cs =======

using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;

namespace MechanicShop.Application.Features.RepairTasks.DTOs;

public record RepairTaskDto(
    Guid RepairTaskId,
    string Name,
    RepairDurationInMinutes EstimatedDurationInMins,
    decimal LaborCost,
    List<PartsDto> Parts,
    decimal TotalCost
);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Mappers\RepairTaskMapper.cs =======

using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.RepairTasks;

namespace MechanicShop.Application.Features.RepairTasks.Mappers;

public static class RepairTaskMapper
{
    public static RepairTaskDto ToDto(this RepairTask repairTask)
    {
        ArgumentNullException.ThrowIfNull(repairTask);
        return new RepairTaskDto(
            repairTask.Id,
            repairTask.Name,
            repairTask.EstimatedDurationInMins,
            repairTask.LaborCost,
            repairTask.Parts.Select(p => p.ToDto()).ToList() ?? [],
            repairTask.TotalCost
        );
    }

    public static PartsDto ToDto(this Part part)
    {
        ArgumentNullException.ThrowIfNull(part);

        return new PartsDto(part.Name, part.Cost, part.Quantity);
    }

    public static List<RepairTaskDto> ToDtos(this IEnumerable<RepairTask> repairTasks)
    {
        return [.. repairTasks.Select(p => p.ToDto())];
    }

    public static IEnumerable<PartsDto> ToDtos(this IEnumerable<Part> parts)
    {
        return [.. parts.Select(p => p.ToDto())];
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Queries\GetRepairTaskById\GetRepairTaskByIdQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;

public sealed record GetRepairTaskByIdQuery(Guid RepairTaskId) : ICachedQuery<Result<RepairTaskDto>>
{
    public string CacheKey => $"repair-tasks_{RepairTaskId}";

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);

    public string[] Tags => ["repair-task"];
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Queries\GetRepairTaskById\GetRepairTaskByIdQueryHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;

public class GetRepairTaskByIdQueryHandler(
    ILogger<GetRepairTaskByIdQueryHandler> logger,
    IAppDbContext context
) : IRequestHandler<GetRepairTaskByIdQuery, Result<RepairTaskDto>>
{
    private readonly ILogger<GetRepairTaskByIdQueryHandler> _logger = logger;
    private readonly IAppDbContext _context = context;

    public async Task<Result<RepairTaskDto>> Handle(
        GetRepairTaskByIdQuery query,
        CancellationToken ct
    )
    {
        var repairTask = await _context
            .RepairTasks.AsNoTracking()
            .Include(c => c.Parts)
            .FirstOrDefaultAsync(c => c.Id == query.RepairTaskId, ct);

        if (repairTask is null)
        {
            _logger.LogWarning(
                "Repair task with id {RepairTaskId} was not found",
                query.RepairTaskId
            );

            return ApplicationErrors.RepairTaskNotFound;
        }

        return repairTask.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Queries\GetRepairTaskById\GetRepairTaskByIdQueryValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;

public sealed class GetRepairTaskByIdQueryValidator : AbstractValidator<GetRepairTaskByIdQuery>
{
    public GetRepairTaskByIdQueryValidator()
    {
        RuleFor(request => request.RepairTaskId)
            .NotEmpty()
            .WithErrorCode("RepairTaskId_Is_Required")
            .WithMessage("CustomerId is required.");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Queries\GetRepairTasks\GetRepairTasksQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;

public sealed record GetRepairTasksQuery() : ICachedQuery<Result<List<RepairTaskDto>>>
{
    public string CacheKey => "repair-tasks";

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);

    public string[] Tags => ["repair-task"];
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\RepairTasks\Queries\GetRepairTasks\GetRepairTasksQueryHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;

public class GetRepairTasksQueryHandler(IAppDbContext context)
    : IRequestHandler<GetRepairTasksQuery, Result<List<RepairTaskDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<List<RepairTaskDto>>> Handle(
        GetRepairTasksQuery query,
        CancellationToken ct
    )
    {
        var repairTasks = await _context
            .RepairTasks.Include(rt => rt.Parts)
            .AsNoTracking()
            .ToListAsync(ct);

        return repairTasks.ToDtos();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Scheduling\DTOs\AvailabilitySlotDto.cs =======

using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.Scheduling.DTOs;

public sealed class AvailabilitySlotDto
{
    public Guid? WorkOrderId { get; set; }
    public Spot Spot { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string? Vehicle { get; set; }
    public EmployeeDto? Labor { get; set; }
    public bool IsOccupied { get; set; }
    public bool? IsAvailable { get; set; }
    public bool WorkOrderLocked { get; set; }
    public WorkOrderState? State { get; set; }
    public RepairTaskDto[]? RepairTasks { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Scheduling\DTOs\ScheduleDto.cs =======

namespace MechanicShop.Application.Features.Scheduling.DTOs;

public sealed record ScheduleDto(DateOnly OnDate, bool EndDay, List<SpotDto> Spots);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Scheduling\DTOs\SpotDto.cs =======

using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.Scheduling.DTOs;

public sealed record SpotDto(Spot Spot, List<AvailabilitySlotDto> AvailabilitySlots);

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Scheduling\Queries\GetDailyScheduleQuery\GetDailyScheduleQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Scheduling.DTOs;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery;

public sealed record GetDailyScheduleQuery(
    TimeZoneInfo TimeZone,
    DateOnly ScheduleDate,
    Guid? LaborId
) : ICachedQuery<Result<ScheduleDto>>
{
    public string CacheKey =>
        $"work-order:{ScheduleDate:yyyy-MM-dd}:labor={LaborId?.ToString() ?? "-"}";
    public string[] Tags => ["work-order"];
    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\Scheduling\Queries\GetDailyScheduleQuery\GetDailyScheduleQueryHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.Mapper;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Application.Features.Scheduling.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery;

public sealed class GetDailyScheduleQueryHandler(IAppDbContext Context, TimeProvider TimeProvider)
    : IRequestHandler<GetDailyScheduleQuery, Result<ScheduleDto>>
{
    private readonly IAppDbContext _context = Context;

    private readonly TimeProvider _timeProvider = TimeProvider;

    public async Task<Result<ScheduleDto>> Handle(GetDailyScheduleQuery query, CancellationToken ct)
    {
        var localStart = query.ScheduleDate.ToDateTime(TimeOnly.MinValue);
        var localEnd = localStart.AddDays(1);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, query.TimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, query.TimeZone);

        var now = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), query.TimeZone);

        var workOrders = await _context
            .WorkOrders.AsNoTracking()
            .Where(wo =>
                wo.StartAtUtc < endUtc
                && wo.EndAtUtc > startUtc
                && (query.LaborId == null || wo.LaborId == query.LaborId)
            )
            .Include(w => w.RepairTasks)
            .Include(w => w.Vehicle)
            .Include(w => w.Labor)
            .ToListAsync(ct);

        var result = new ScheduleDto(query.ScheduleDate, now < endUtc, []);

        foreach (var spot in Enum.GetValues<Spot>())
        {
            var current = localStart;
            var slots = new List<AvailabilitySlotDto>();

            var woBySpot = workOrders
                .Where(w => w.Spot == spot)
                .OrderBy(w => w.StartAtUtc)
                .ToList();

            while (current < endUtc)
            {
                var next = current.AddMinutes(15);
                var utcStart = TimeZoneInfo.ConvertTimeToUtc(current, query.TimeZone);
                var utcEnd = TimeZoneInfo.ConvertTimeToUtc(next, query.TimeZone);
                var workOrder = woBySpot.FirstOrDefault(wo =>
                    wo.StartAtUtc < utcEnd && wo.EndAtUtc > utcStart
                );

                if (workOrder != null)
                {
                    if (!slots.Any(s => s.WorkOrderId == workOrder.Id))
                    {
                        slots.Add(
                            new AvailabilitySlotDto
                            {
                                WorkOrderId = workOrder.Id,
                                Spot = spot,
                                StartAt = workOrder.StartAtUtc,
                                EndAt = workOrder.EndAtUtc,
                                Vehicle = FormatVehicleInfo(workOrder.Vehicle!),
                                Labor = workOrder.Labor!.ToDto(),
                                IsOccupied = true,
                                RepairTasks =
                                [
                                    .. workOrder.RepairTasks.ToList().ConvertAll(rt => rt.ToDto()),
                                ],
                                WorkOrderLocked = !workOrder.IsEditable,
                                State = workOrder.State,
                                IsAvailable = false,
                            }
                        );
                    }
                }
                else
                {
                    slots.Add(
                        new AvailabilitySlotDto
                        {
                            Spot = spot,
                            StartAt = startUtc,
                            EndAt = endUtc,
                            WorkOrderLocked = false,
                            IsOccupied = false,
                            IsAvailable = current >= now,
                        }
                    );
                }
                current = next;
            }
            result.Spots.Add(new SpotDto(spot, slots));
        }
        return result;
    }

    private static string? FormatVehicleInfo(Vehicle vehicle) =>
        vehicle != null ? $"{vehicle.Make} | {vehicle.LicensePlate}" : null;
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\AssignLabor\AssignLaborCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.AssignLabor;

public sealed record AssignLaborCommand(Guid WorkOrderId, Guid LaborId) : IRequest<Result<Updated>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\AssignLabor\AssignLaborCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.AssignLabor;

public sealed class AssignLaborCommandHandler(
    IAppDbContext Context,
    ILogger<AssignLaborCommandHandler> Logger,
    HybridCache Cache,
    IWorkOrderPolicy WorkOrderPolicy
) : IRequestHandler<AssignLaborCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<AssignLaborCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly IWorkOrderPolicy _workOrderPolicy = WorkOrderPolicy;

    public async Task<Result<Updated>> Handle(AssignLaborCommand command, CancellationToken ct)
    {
        var workOrder = await _context.WorkOrders.FirstOrDefaultAsync(
            wo => wo.Id == command.WorkOrderId,
            ct
        );
        if (workOrder is null)
        {
            _logger.LogWarning("Work order with id: {WorkOrderId} not found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        var laborIsExist = await _context.Employees.AnyAsync(
            e => e.Id == command.LaborId && e.Role == Role.Labor,
            ct
        );

        if (!laborIsExist)
        {
            _logger.LogWarning("Labor with id: {LaborId} not found", command.LaborId);
            return ApplicationErrors.LaborNotFound;
        }
        var isLaborOccupied = await _workOrderPolicy.IsLaborOccupied(
            command.LaborId,
            workOrder.StartAtUtc,
            workOrder.EndAtUtc,
            command.WorkOrderId
        );

        var updateLaborResult = workOrder.UpdateLabor(command.LaborId);

        if (updateLaborResult.IsError)
        {
            foreach (var error in updateLaborResult.Errors)
            {
                _logger.LogError(
                    "[LaborUpdate] {ErrorCode}: {ErrorDescription}",
                    error.Code,
                    error.Description
                );
            }
            return updateLaborResult.Errors;
        }

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("work-order", ct);
        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\AssignLabor\AssignLaborCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.AssignLabor;

public sealed class AssignLaborCommandValidator : AbstractValidator<AssignLaborCommand>
{
    public AssignLaborCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotEmpty()
            .WithErrorCode("WorkOrderId_Required")
            .WithMessage("WorkOrderId is required.");

        RuleFor(x => x.LaborId)
            .NotEmpty()
            .WithErrorCode("LaborId_Required")
            .WithMessage("LaborId is required.");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\CreateWorkOrder\CreateWorkOrderCommand.cs =======

using MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;
using MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed record CreateWorkOrderCommand(
    DateTimeOffset StartAt,
    Guid LaborId,
    Guid VehicleId,
    Spot Spot,
    List<Guid> RepairTaskIds
) : IRequest<Result<WorkOrderDto>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\CreateWorkOrder\CreateWorkOrderCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Application.Features.WorkOrders.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandHandler(
    IAppDbContext Context,
    ILogger<CreateWorkOrderCommandHandler> Logger,
    HybridCache Cache,
    IWorkOrderPolicy workOrderValidator,
    TimeProvider TimeProvider
) : IRequestHandler<CreateWorkOrderCommand, Result<WorkOrderDto>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<CreateWorkOrderCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly IWorkOrderPolicy _workOrderValidator = workOrderValidator;
    private readonly TimeProvider _timeProvider = TimeProvider;

    public async Task<Result<WorkOrderDto>> Handle(
        CreateWorkOrderCommand command,
        CancellationToken ct
    )
    {
        var repairTasks = await _context
            .RepairTasks.Where(rt => command.RepairTaskIds.Contains(rt.Id))
            .ToListAsync(ct);

        if (repairTasks.Count != command.RepairTaskIds.Count)
        {
            var missingIds = command
                .RepairTaskIds.Except(repairTasks.Select(rt => rt.Id))
                .ToArray();
            _logger.LogWarning(
                "Some RepairTaskIds not found: {MissingIds}",
                string.Join(", ", missingIds)
            );
            return ApplicationErrors.RepairTaskNotFound;
        }
        var totalEstimatedDuration = TimeSpan.FromMinutes(
            repairTasks.Sum(tr => (int)tr.EstimatedDurationInMins)
        );

        var endAt = command.StartAt.Add(totalEstimatedDuration);

        if (_workOrderValidator.IsOutsideOperatingHours(command.StartAt, endAt))
        {
            _logger.LogWarning(
                "The WorkOrder time ({StartAt} ? {EndAt}) is outside of store operating hours.",
                command.StartAt,
                endAt
            );

            return ApplicationErrors.WorkOrderOutsideOperatingHour(command.StartAt, endAt);
        }

        var checkMinRequirementResult = _workOrderValidator.ValidateMinimumRequirement(
            command.StartAt,
            endAt
        );

        if (checkMinRequirementResult.IsError)
        {
            _logger.LogWarning("WorkOrder duration is shorter than the configured minimum.");

            return checkMinRequirementResult.Errors;
        }

        var checkSpotAvailabilityResult = await _workOrderValidator.CheckSpotAvailabilityAsync(
            command.Spot,
            command.StartAt,
            endAt,
            excludeWorkOrderId: null,
            ct
        );

        if (checkSpotAvailabilityResult.IsError)
        {
            _logger.LogWarning("Spot: {Spot} is not available.", command.Spot.ToString());
            return checkSpotAvailabilityResult.Errors;
        }

        var vehicle = await _context
            .Vehicles.Include(v => v.Customer)
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, ct);

        if (vehicle is null)
        {
            _logger.LogWarning("Vehicle with Id '{VehicleId}' does not exist.", command.VehicleId);

            return ApplicationErrors.VehicleNotFound;
        }

        var labor = await _context.Employees.FirstOrDefaultAsync(
            e => e.Id == command.LaborId && e.Role == Role.Labor,
            ct
        );

        if (labor is null)
        {
            _logger.LogWarning("Invalid LaborId: {LaborId}", command.LaborId.ToString());
            return ApplicationErrors.LaborNotFound;
        }
        var vehicleIsSchedule = await _workOrderValidator.IsVehicleAlreadyScheduled(
            vehicle.Id,
            command.StartAt,
            endAt,
            null
        );

        var laborIsOccupied = await _workOrderValidator.IsLaborOccupied(
            command.LaborId,
            command.StartAt,
            endAt,
            null
        );

        var createWorkOrderResult = WorkOrder.Create(
            Guid.NewGuid(),
            command.VehicleId,
            command.StartAt,
            endAt,
            command.LaborId,
            command.Spot,
            repairTasks,
            _timeProvider
        );

        if (createWorkOrderResult.IsError)
        {
            _logger.LogWarning(
                "Failed to create WorkOrder: {Error}",
                createWorkOrderResult.TopError.Description
            );

            return createWorkOrderResult.Errors;
        }

        var workOrder = createWorkOrderResult.Value;

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "WorkOrder with Id '{WorkOrderId}' created successfully.",
            workOrder.Id
        );

        await _cache.RemoveByTagAsync("work-order", ct);

        return workOrder.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\CreateWorkOrder\CreateWorkOrderCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.VehicleId).NotEmpty().WithMessage("VehicleId is required.");

        RuleFor(request => request.StartAt)
            .Must((request, startAt) => startAt > timeProvider.GetUtcNow())
            .WithErrorCode("StartAt_Past")
            .WithMessage("StartAt must be in the future.");

        RuleFor(request => request.RepairTaskIds)
            .NotEmpty()
            .WithMessage("At least one repair task must be selected");

        RuleFor(request => request.LaborId)
            .NotEmpty()
            .WithErrorCode("Labor_Required")
            .WithMessage("LaborId is required and must not be empty.");

        RuleFor(x => x.Spot)
            .IsInEnum()
            .WithErrorCode("Spot_Invalid")
            .WithMessage("Spot must be a valid Spot value. [A, B, C, D]");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\RelocateWorkOrder\RelocateWorkOrderCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RelocateWorkOrder;

public sealed record RelocateWorkOrderCommand(
    Guid WorkOrderId,
    DateTimeOffset NewStartAt,
    Spot NewSpot
) : IRequest<Result<Updated>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\RelocateWorkOrder\RelocateWorkOrderCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RelocateWorkOrder;

public sealed class RelocateWorkOrderCommandHandler(
    IAppDbContext Context,
    ILogger<RelocateWorkOrderCommandHandler> Logger,
    HybridCache Cache,
    TimeProvider TimeProvider,
    IWorkOrderPolicy WorkOrderPolicy
) : IRequestHandler<RelocateWorkOrderCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<RelocateWorkOrderCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly TimeProvider _timeProvider = TimeProvider;
    private readonly IWorkOrderPolicy _workOrderPolicy = WorkOrderPolicy;

    public async Task<Result<Updated>> Handle(
        RelocateWorkOrderCommand command,
        CancellationToken ct
    )
    {
        var workOrder = await _context
            .WorkOrders.Include(a => a.RepairTasks)
            .Include(a => a.Labor)
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.Id == command.WorkOrderId, ct);

        if (workOrder is null)
        {
            _logger.LogWarning("Work order with id: {WorkOrderId} not found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        var duration = TimeSpan.FromMinutes(
            workOrder.RepairTasks.Sum(rt => (int)rt.EstimatedDurationInMins)
        );
        var endAt = command.NewStartAt.Add(duration);

        var spotIsAvailable = await _workOrderPolicy.CheckSpotAvailabilityAsync(
            command.NewSpot,
            command.NewStartAt,
            endAt,
            command.WorkOrderId
        );
        if (spotIsAvailable.IsError)
        {
            _logger.LogWarning("Spot: {Spot} is not available.", workOrder.Spot.ToString());
            return spotIsAvailable.Errors;
        }
        var laborIsOccupied = await _workOrderPolicy.IsLaborOccupied(
            workOrder.LaborId,
            command.NewStartAt,
            endAt,
            command.WorkOrderId
        );
        var vehicleIsScheduled = await _workOrderPolicy.IsVehicleAlreadyScheduled(
            workOrder.VehicleId,
            command.NewStartAt,
            endAt,
            command.WorkOrderId
        );
        var updateTimingResult = workOrder.UpdateTiming(command.NewStartAt, endAt, _timeProvider);
        if (updateTimingResult.IsError)
        {
            _logger.LogError(
                "Failed to update timing: {Error}",
                updateTimingResult.TopError.Description
            );

            return updateTimingResult.Errors;
        }
        var updateSpotResult = workOrder.UpdateSpot(command.NewSpot);

        if (updateSpotResult.IsError)
        {
            _logger.LogError(
                "Failed to update Spot: {Error}",
                updateSpotResult.TopError.Description
            );

            return updateTimingResult.Errors;
        }

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("work-order", ct);

        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\RelocateWorkOrder\RelocateWorkOrderCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RelocateWorkOrder;

public sealed class RescheduleAppointmentCommandValidator
    : AbstractValidator<RelocateWorkOrderCommand>
{
    public RescheduleAppointmentCommandValidator(TimeProvider TimeProvider)
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();

        RuleFor(x => x.NewStartAt)
            .GreaterThan(TimeProvider.GetUtcNow())
            .WithMessage("New start time must be in the future.");

        RuleFor(x => x.NewSpot).IsInEnum();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\RemoveWorkOrder\RemoveWorkOrderCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RemoveWorkOrder;

public sealed record DeleteWorkOrderCommand(Guid WorkOrderId) : IRequest<Result<Deleted>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\RemoveWorkOrder\RemoveWorkOrderCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RemoveWorkOrder;

public sealed class DeleteWorkOrderCommandHandler(
    ILogger<DeleteWorkOrderCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache
) : IRequestHandler<DeleteWorkOrderCommand, Result<Deleted>>
{
    private readonly ILogger<DeleteWorkOrderCommandHandler> _logger = logger;
    private readonly IAppDbContext _context = context;
    private readonly HybridCache _cache = cache;

    public async Task<Result<Deleted>> Handle(DeleteWorkOrderCommand command, CancellationToken ct)
    {
        var workOrder = await _context.WorkOrders.FindAsync([command.WorkOrderId], ct);

        if (workOrder is null)
        {
            _logger.LogError(
                "WorkOrder with Id '{WorkOrderId}' does not exist.",
                command.WorkOrderId
            );

            return ApplicationErrors.WorkOrderNotFound;
        }

        var deleteResult = workOrder.Delete();

        if (deleteResult.IsError)
        {
            _logger.LogError(
                "Deletion failed: only 'Scheduled' or 'Confirmed' WorkOrders can be deleted. Current status: {Status}",
                workOrder.State
            );
            return deleteResult.Errors;
        }

        _context.WorkOrders.Remove(workOrder);

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("work-order", ct);

        return Result.Deleted;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\RemoveWorkOrder\RemoveWorkOrderCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RemoveWorkOrder;

public sealed class DeleteWorkOrderCommandValidator : AbstractValidator<DeleteWorkOrderCommand>
{
    public DeleteWorkOrderCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotEmpty()
            .WithErrorCode("WorkOrderId_Required")
            .WithMessage("WorkOrderId is required.");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\UpdateWorkOrderRepairTasks\UpdateWorkOrderRepairTasksCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

public sealed record UpdateWorkOrderRepairTasksCommand(Guid WorkOrderId, Guid[] RepairTasksIds)
    : IRequest<Result<Updated>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\UpdateWorkOrderRepairTasks\UpdateWorkOrderRepairTasksCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

public sealed class UpdateWorkOrderRepairTasksCommandHandler(
    IAppDbContext Context,
    ILogger<UpdateWorkOrderRepairTasksCommandHandler> Logger,
    HybridCache Cache,
    IWorkOrderPolicy WorkOrderPolicy,
    TimeProvider TimeProvider
) : IRequestHandler<UpdateWorkOrderRepairTasksCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<UpdateWorkOrderRepairTasksCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly IWorkOrderPolicy _workOrderPolicy = WorkOrderPolicy;
    private readonly TimeProvider _timeProvider = TimeProvider;

    public async Task<Result<Updated>> Handle(
        UpdateWorkOrderRepairTasksCommand command,
        CancellationToken ct
    )
    {
        var workOrder = await _context
            .WorkOrders.Include(wo => wo.RepairTasks)
            .FirstOrDefaultAsync(wo => wo.Id == command.WorkOrderId);

        if (workOrder is null)
        {
            _logger.LogWarning("Work order with id: {WorkOrderId} not found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        if (command.RepairTasksIds.Length == 0)
        {
            _logger.LogError("Empty RepairTaskIds list submitted.");

            return RepairTaskErrors.AtLeastOneRepairTaskIsRequired;
        }

        var repairTasks = await _context
            .RepairTasks.Where(t => command.RepairTasksIds.Contains(t.Id))
            .ToListAsync(ct);

        if (repairTasks.Count != command.RepairTasksIds.Count())
        {
            var missingIds = command
                .RepairTasksIds.Except(repairTasks.Select(rt => rt.Id))
                .ToArray();
            _logger.LogWarning(
                "Some RepairTaskIds not found: {MissingIds}",
                string.Join(", ", missingIds)
            );
            return ApplicationErrors.RepairTaskNotFound;
        }

        var startAt = workOrder.StartAtUtc;
        var duration = TimeSpan.FromMinutes(repairTasks.Sum(rt => (int)rt.EstimatedDurationInMins));
        var endAt = startAt.Add(duration);

        if (_workOrderPolicy.IsOutsideOperatingHours(startAt, endAt))
        {
            _logger.LogWarning(
                "The WorkOrder time ({StartAt} ? {EndAt}) is outside of store operating hours.",
                startAt,
                endAt
            );
            return ApplicationErrors.WorkOrderOutsideOperatingHour(startAt, endAt);
        }

        var addRepairTaskResult = workOrder.UpdateRepairTasks(repairTasks);
        if (addRepairTaskResult.IsError)
        {
            return addRepairTaskResult;
        }

        var spotCheckResult = await _workOrderPolicy.CheckSpotAvailabilityAsync(
            workOrder.Spot,
            workOrder.StartAtUtc,
            endAt,
            excludeWorkOrderId: workOrder.Id,
            ct: ct
        );

        if (spotCheckResult.IsError)
        {
            return spotCheckResult.Errors;
        }

        var laborIsOccupied = await _workOrderPolicy.IsLaborOccupied(
            workOrder.LaborId,
            workOrder.StartAtUtc,
            endAt,
            workOrder.Id
        );

        var timingResult = workOrder.UpdateTiming(workOrder.StartAtUtc, endAt, _timeProvider);

        if (timingResult.IsError)
        {
            return timingResult.Errors;
        }
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Repair tasks added successfully");
        await _cache.RemoveByTagAsync("work-order", ct);
        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\UpdateWorkOrderRepairTasks\UpdateWorkOrderRepairTasksCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

public sealed class UpdateWorkOrderRepairTasksCommandValidator
    : AbstractValidator<UpdateWorkOrderRepairTasksCommand>
{
    public UpdateWorkOrderRepairTasksCommandValidator()
    {
        RuleFor(wo => wo.WorkOrderId).NotEmpty().WithMessage("Work order ID is required.");

        RuleFor(wo => wo.RepairTasksIds)
            .NotEmpty()
            .WithMessage("Repair task ID is required, should be at least one.");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\UpdateWorkOrderState\UpdateWorkOrderStateCommand.cs =======

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderState;

public sealed record UpdateWorkOrderStateCommand(Guid WorkOrderId, WorkOrderState WorkOrderState)
    : IRequest<Result<Updated>>;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\UpdateWorkOrderState\UpdateWorkOrderStateCommandHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderState;

public sealed class UpdateWorkOrderStateCommandHandler(
    IAppDbContext Context,
    ILogger<UpdateWorkOrderStateCommandHandler> Logger,
    HybridCache Cache,
    TimeProvider TimeProvider
) : IRequestHandler<UpdateWorkOrderStateCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<UpdateWorkOrderStateCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly TimeProvider _timeProvider = TimeProvider;

    public async Task<Result<Updated>> Handle(
        UpdateWorkOrderStateCommand command,
        CancellationToken ct
    )
    {
        var workOrder = await _context.WorkOrders.FindAsync([command.WorkOrderId], ct);
        if (workOrder is null)
        {
            _logger.LogWarning("Work order with id: {WorkOrderId} not found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        if (
            command.WorkOrderState != WorkOrderState.Cancelled
            && workOrder.StartAtUtc > _timeProvider.GetUtcNow()
        )
        {
            _logger.LogWarning(
                "State transition for WorkOrder Id '{WorkOrderId}` is not allowed before the work orderï¿½s scheduled start time.",
                command.WorkOrderId
            );

            return WorkOrderErrors.StateTransitionNotAllowed(workOrder.StartAtUtc);
        }

        var updateStatusResult = workOrder.UpdateState(command.WorkOrderState);

        if (updateStatusResult.IsError)
        {
            _logger.LogError(
                "Failed to update status: {Error}",
                updateStatusResult.TopError.Description
            );
            return updateStatusResult.Errors;
        }

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("work-order", ct);
        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Commands\UpdateWorkOrderState\UpdateWorkOrderStateCommandValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderState;

public sealed class UpdateWorkOrderStateCommandValidator
    : AbstractValidator<UpdateWorkOrderStateCommand>
{
    public UpdateWorkOrderStateCommandValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty().WithMessage("Work order id is required.");

        RuleFor(x => x.WorkOrderState)
            .IsInEnum()
            .WithErrorCode("WorkOrderStatus_Invalid")
            .WithMessage("Status must be a valid WorkOrderStatus value.");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\DTOs\WorkOrderDto.cs =======

using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.WorkOrders.DTOs;

public sealed class WorkOrderDto
{
    public Guid WorkOrderId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Spot Spot { get; set; }
    public VehicleDto? Vehicle { get; set; }
    public DateTimeOffset StartAtUtc { get; set; }
    public DateTimeOffset EndAtUtc { get; set; }
    public List<RepairTaskDto> RepairTasks { get; set; } = [];
    public EmployeeDto? Labor { get; set; }
    public WorkOrderState State { get; set; }
    public decimal TotalPartCost { get; set; }
    public decimal TotalLaborCost { get; set; }
    public decimal TotalCost { get; set; }
    public int TotalDurationInMins { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\DTOs\WorkOrderListItemDto.cs =======

using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.WorkOrders.DTOs;

public class WorkOrderListItemDto
{
    public Guid WorkOrderId { get; set; }
    public Guid? InvoiceId { get; set; }
    public VehicleDto? Vehicle { get; set; } = default!;
    public string? Customer { get; set; }
    public string? Labor { get; set; }
    public WorkOrderState State { get; set; }
    public Spot Spot { get; set; }
    public DateTimeOffset StartAtUtc { get; set; }
    public DateTimeOffset EndAtUtc { get; set; }
    public List<string> RepairTasks { get; set; } = [];
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Events\WorkOrderCollectionModifiedEventHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.WorkOrders.Events;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.EventHandlers;

public sealed class WorkOrderDashboardNotifierHandler(IWorkOrderNotifier notifier)
    : INotificationHandler<WorkOrderCreated>,
        INotificationHandler<WorkOrderCompleted>,
        INotificationHandler<WorkOrderCancelled>,
        INotificationHandler<WorkOrderRescheduled>,
        INotificationHandler<WorkOrderSpotUpdated>,
        INotificationHandler<WorkOrderLaborReassigned>,
        INotificationHandler<WorkOrderRepairTasksUpdated>,
        INotificationHandler<WorkOrderRemoved>
{
    private readonly IWorkOrderNotifier _notifier = notifier;

    public Task Handle(WorkOrderCreated notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderCreated), ct);

    public Task Handle(WorkOrderCompleted notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderCompleted), ct);

    public Task Handle(WorkOrderCancelled notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderCancelled), ct);

    public Task Handle(WorkOrderRescheduled notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderRescheduled), ct);

    public Task Handle(WorkOrderSpotUpdated notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderSpotUpdated), ct);

    public Task Handle(WorkOrderLaborReassigned notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderLaborReassigned), ct);

    public Task Handle(WorkOrderRepairTasksUpdated notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderRepairTasksUpdated), ct);

    public Task Handle(WorkOrderRemoved notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderRemoved), ct);

    private Task NotifyAsync(Guid workOrderId, string eventType, CancellationToken ct)
    {
        return _notifier.NotifyWorkOrdersChangedAsync(workOrderId, eventType, ct);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Mappers\WorkOrderMapper.cs =======

using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.WorkOrders.Mappers;

public static class WorkOrderMapper
{
    public static WorkOrderDto ToDto(this WorkOrder entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new WorkOrderDto
        {
            WorkOrderId = entity.Id,
            Spot = entity.Spot,
            StartAtUtc = entity.StartAtUtc,
            EndAtUtc = entity.EndAtUtc,
            Labor = entity.Labor is null
                ? null
                : new EmployeeDto
                {
                    Id = entity.LaborId,
                    FirstName = entity.Labor.FirstName,
                    LastName = entity.Labor.LastName,
                    FullName = $"{entity.Labor.FirstName} {entity.Labor.LastName}",
                },
            RepairTasks = entity.RepairTasks.ToDtos(),
            Vehicle = entity.Vehicle is null ? null : entity.Vehicle.ToDto(),
            State = entity.State,
            TotalPartCost = entity
                .RepairTasks.SelectMany(t => t.Parts)
                .Sum(p => p.Cost * p.Quantity),
            TotalLaborCost = entity.RepairTasks.Sum(p => p.LaborCost),
            TotalCost = entity.RepairTasks.Sum(rt => rt.TotalCost),
            TotalDurationInMins = entity.RepairTasks.Sum(rt => (int)rt.EstimatedDurationInMins),
            InvoiceId = entity.Invoice?.Id,
            CreatedAt = entity.CreateAtUtc,
        };
    }

    public static IEnumerable<WorkOrderDto> ToDtos(this IEnumerable<WorkOrder> entities)
    {
        return [.. entities.Select(e => e.ToDto())];
    }

    public static WorkOrderListItemDto ToListItemDto(this WorkOrder entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new WorkOrderListItemDto
        {
            WorkOrderId = entity.Id,
            Spot = entity.Spot,
            StartAtUtc = entity.StartAtUtc,
            EndAtUtc = entity.EndAtUtc,
            Vehicle = entity.Vehicle!.ToDto(),
            Labor = entity.Labor is null
                ? null
                : $"{entity.Labor.FirstName} {entity.Labor.LastName}",
            State = entity.State,
            RepairTasks = entity.RepairTasks.Select(rt => rt.Name).ToList(),
            Customer = entity.Vehicle!.Customer!.Name,
            InvoiceId = entity.Invoice == null ? null : entity.Invoice.Id,
        };
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Queries\GetWorkOrderByIdQuery\GetAppointmentByIdQueryValidator.cs =======

using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;

public sealed class GetAppointmentByIdQueryValidator : AbstractValidator<GetWorkOrderByIdQuery>
{
    public GetAppointmentByIdQueryValidator()
    {
        RuleFor(request => request.WorkOrderId)
            .NotEmpty()
            .WithErrorCode("WorkOrderId_Is_Required")
            .WithMessage("WorkOrderId is required.");
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Queries\GetWorkOrderByIdQuery\GetWorkOrderByIdQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;

public sealed record GetWorkOrderByIdQuery(Guid WorkOrderId) : ICachedQuery<Result<WorkOrderDto>>
{
    public string CacheKey => $"work-order:{WorkOrderId}";
    public string[] Tags => ["work-order"];
    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Queries\GetWorkOrderByIdQuery\GetWorkOrderByIdQueryHandler.cs =======

using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Application.Features.WorkOrders.Mappers;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;

public sealed class GetWorkOrderByIdQueryHandler(
    ILogger<GetWorkOrderByIdQueryHandler> logger,
    IAppDbContext context
) : IRequestHandler<GetWorkOrderByIdQuery, Result<WorkOrderDto>>
{
    private readonly ILogger<GetWorkOrderByIdQueryHandler> _logger = logger;
    private readonly IAppDbContext _context = context;

    public async Task<Result<WorkOrderDto>> Handle(
        GetWorkOrderByIdQuery query,
        CancellationToken ct
    )
    {
        var workOrder = await _context
            .WorkOrders.AsNoTracking()
            .Include(a => a.RepairTasks)
                .ThenInclude(a => a.Parts)
            .Include(a => a.Labor)
            .Include(a => a.Vehicle!)
                .ThenInclude(v => v.Customer)
            .Include(a => a.Invoice)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == query.WorkOrderId, ct);

        if (workOrder is null)
        {
            _logger.LogWarning("WorkOrder with id {WorkOrderId} was not found", query.WorkOrderId);

            return ApplicationErrors.WorkOrderNotFound;
        }

        return workOrder.ToDto();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Queries\GetWorkOrders\GetWorkOrdersQuery.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;

public sealed record GetWorkOrdersQuery(
    int Page,
    int PageSize,
    string? SearchTerm,
    string SortColumn = "createdAt",
    string SortDirection = "desc",
    WorkOrderState? State = null,
    Guid? VehicleId = null,
    Guid? LaborId = null,
    DateTime? StartDateFrom = null,
    DateTime? StartDateTo = null,
    DateTime? EndDateFrom = null,
    DateTime? EndDateTo = null,
    Spot? Spot = null
) : ICachedQuery<Result<PaginatedList<WorkOrderListItemDto>>>
{
    public string CacheKey =>
        $"work-orders:p={Page}:ps={PageSize}"
        + $":q={SearchTerm ?? "-"}"
        + $":sort={SortColumn}:{SortDirection}"
        + $":state={State?.ToString() ?? "-"}"
        + $":veh={VehicleId?.ToString() ?? "-"}"
        + $":lab={LaborId?.ToString() ?? "-"}"
        + $":sdfrom={StartDateFrom?.ToString("yyyyMMdd") ?? "-"}"
        + $":sdto={StartDateTo?.ToString("yyyyMMdd") ?? "-"}"
        + $":edfrom={EndDateFrom?.ToString("yyyyMMdd") ?? "-"}"
        + $":edto={EndDateTo?.ToString("yyyyMMdd") ?? "-"}"
        + $":spot={Spot?.ToString() ?? "-"}";

    public string[] Tags => ["work-order"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Application\Features\WorkOrders\Queries\GetWorkOrders\GetWorkOrdersQueryHandler.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;

public sealed class GetWorkOrdersQueryHandler(IAppDbContext Context)
    : IRequestHandler<GetWorkOrdersQuery, Result<PaginatedList<WorkOrderListItemDto>>>
{
    private readonly IAppDbContext _context = Context;

    public async Task<Result<PaginatedList<WorkOrderListItemDto>>> Handle(
        GetWorkOrdersQuery query,
        CancellationToken ct
    )
    {
        var workOrders = _context.WorkOrders.AsNoTracking().AsQueryable();

        workOrders = ApplyFilters(workOrders, query);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            workOrders = ApplySearch(workOrders, query.SearchTerm);
        }
        workOrders = ApplySort(workOrders, query.SortColumn, query.SortDirection);

        var count = await workOrders.CountAsync(ct);

        var items = await workOrders
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(wo => new WorkOrderListItemDto
            {
                WorkOrderId = wo.Id,
                InvoiceId = wo.Invoice == null ? null : wo.Invoice.Id,
                Spot = wo.Spot,
                StartAtUtc = wo.StartAtUtc,
                EndAtUtc = wo.EndAtUtc,
                Vehicle = new VehicleDto(
                    wo.Vehicle!.CustomerId,
                    wo.VehicleId,
                    wo.Vehicle.Make,
                    wo.Vehicle.Model,
                    wo.Vehicle.Year,
                    wo.Vehicle.LicensePlate
                ),
                Customer =
                    wo.Vehicle != null && wo.Vehicle.Customer != null
                        ? wo.Vehicle.Customer.Name
                        : null,
                Labor = wo.Labor != null ? wo.Labor.FirstName + " " + wo.Labor.LastName : null,
                State = wo.State,
                RepairTasks = wo.RepairTasks.Select(rt => rt.Name).ToList(),
            })
            .ToListAsync(ct);

        return new PaginatedList<WorkOrderListItemDto>
        {
            Items = items,
            PageNumber = query.Page,
            PageSize = query.PageSize,
            TotalCount = count,
            TotalPages = (int)Math.Ceiling(count / (double)query.PageSize),
        };
    }

    private IQueryable<WorkOrder> ApplyFilters(
        IQueryable<WorkOrder> workOrders,
        GetWorkOrdersQuery filterQuery
    )
    {
        if (filterQuery.State.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.State == filterQuery.State);
        }
        if (filterQuery.VehicleId.HasValue && filterQuery.VehicleId != Guid.Empty)
        {
            workOrders = workOrders.Where(wo => wo.VehicleId == filterQuery.VehicleId.Value);
        }

        if (filterQuery.LaborId.HasValue && filterQuery.LaborId != Guid.Empty)
        {
            workOrders = workOrders.Where(wo => wo.LaborId == filterQuery.LaborId.Value);
        }

        if (filterQuery.StartDateFrom.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.StartAtUtc >= filterQuery.StartDateFrom.Value);
        }

        if (filterQuery.StartDateTo.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.StartAtUtc <= filterQuery.StartDateTo.Value);
        }

        if (filterQuery.EndDateFrom.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.EndAtUtc >= filterQuery.EndDateFrom.Value);
        }

        if (filterQuery.EndDateTo.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.EndAtUtc <= filterQuery.EndDateTo.Value);
        }

        if (filterQuery.Spot.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.Spot == filterQuery.Spot.Value);
        }
        return workOrders;
    }

    private IQueryable<WorkOrder> ApplySearch(IQueryable<WorkOrder> workOrders, string searchQuery)
    {
        var normalized = searchQuery.Trim().ToLower();

        return workOrders.Where(wo =>
            (
                wo.Vehicle != null
                && (
                    wo.Vehicle.Make.ToLower().Contains(normalized)
                    || wo.Vehicle.Model.ToLower().Contains(normalized)
                    || wo.Vehicle.LicensePlate.ToLower().Contains(normalized)
                )
            )
            || (
                wo.Labor != null
                && (
                    wo.Labor.FirstName.ToLower().Contains(normalized)
                    || wo.Labor.LastName.ToLower().Contains(normalized)
                    || (wo.Labor.FirstName + " " + wo.Labor.LastName).ToLower().Contains(normalized)
                )
            )
            || wo.RepairTasks.Any(rt => rt.Name.ToLower().Contains(normalized))
            || wo.Id.ToString().ToLower().Contains(normalized)
        );
    }

    private IQueryable<WorkOrder> ApplySort(
        IQueryable<WorkOrder> workOrders,
        string columnSort,
        string sortQuery
    )
    {
        var isDescending = sortQuery.Equals("desc", StringComparison.CurrentCultureIgnoreCase);

        return columnSort switch
        {
            "createdat" => isDescending
                ? workOrders.OrderByDescending(wo => wo.CreateAtUtc)
                : workOrders.OrderBy(wo => wo.CreateAtUtc),
            "updatedat" => isDescending
                ? workOrders.OrderByDescending(wo => wo.LastModifiedAtUtc)
                : workOrders.OrderBy(wo => wo.StartAtUtc),
            "startat" => isDescending
                ? workOrders.OrderByDescending(wo => wo.StartAtUtc)
                : workOrders.OrderBy(wo => wo.StartAtUtc),
            "endat" => isDescending
                ? workOrders.OrderByDescending(wo => wo.EndAtUtc)
                : workOrders.OrderBy(wo => wo.EndAtUtc),
            "state" => isDescending
                ? workOrders.OrderByDescending(wo => wo.State)
                : workOrders.OrderBy(wo => wo.State),
            "spot" => isDescending
                ? workOrders.OrderByDescending(wo => wo.Spot)
                : workOrders.OrderBy(wo => wo.Spot),
            "total" => isDescending
                ? workOrders.OrderByDescending(wo => wo.Total)
                : workOrders.OrderBy(wo => wo.Total),
            "vehicleid" => isDescending
                ? workOrders.OrderByDescending(wo => wo.VehicleId)
                : workOrders.OrderBy(wo => wo.VehicleId),
            "laborid" => isDescending
                ? workOrders.OrderByDescending(wo => wo.LaborId)
                : workOrders.OrderBy(wo => wo.LaborId),
            _ => workOrders.OrderByDescending(wo => wo.CreateAtUtc),
        };
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Common\AuditableEntity.cs =======

namespace MechanicShop.Domain.Common;

public abstract class AuditableEntity : Entity
{
    protected AuditableEntity() { }

    protected AuditableEntity(Guid id)
        : base(id) { }

    public DateTimeOffset CreateAtUtc { get; set; }

    public string? CreateBy { get; set; }

    public DateTimeOffset LastModifiedAtUtc { get; set; }

    public string? LastModifiedBy { get; set; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Common\DomainEvent.cs =======

using MediatR;

namespace MechanicShop.Domain.Common;

public interface DomainEvent : INotification;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Common\Entity.cs =======

namespace MechanicShop.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; }

    private readonly List<DomainEvent> _domainEvent = [];
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvent.AsReadOnly();

    protected Entity() { }

    protected Entity(Guid id)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }

    public void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvent.Add(domainEvent);
    }

    public void RemoveDomainEvent(DomainEvent domainEvent)
    {
        _domainEvent.Remove(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvent.Clear();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Common\Constraints\MechanicShopConstraints.cs =======

namespace MechanicShop.Domain.Common.Constraints;

public sealed class MechanicShopConstraints
{
    public const decimal TaxRate = 0.15m;
    public const string SystemUser = "System";
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Common\Results\Error.cs =======

using System.Text.Json.Serialization;

namespace MechanicShop.Domain.Common.Results;

public readonly record struct Error
{
    public string Code { get; }

    public string Description { get; }

    public ErrorKind Type { get; }

    [JsonConstructor]
    public Error(string code, string description, ErrorKind type)
    {
        Code = code;
        Description = description;
        Type = type;
    }

    public static Error Failure(
        string code = nameof(Failure),
        string description = "General failure"
    ) => new(code, description, ErrorKind.Failure);

    public static Error Unexpected(
        string code = nameof(Unexpected),
        string description = "Unexpected error"
    ) => new(code, description, ErrorKind.Unexpected);

    public static Error Validation(
        string code = nameof(Validation),
        string description = "Validation error"
    ) => new(code, description, ErrorKind.Validation);

    public static Error Conflict(
        string code = nameof(Conflict),
        string description = "Conflict error"
    ) => new(code, description, ErrorKind.Conflict);

    public static Error NotFound(
        string code = nameof(NotFound),
        string description = "Not found error"
    ) => new(code, description, ErrorKind.NotFound);

    public static Error Unauthorized(
        string code = nameof(Unauthorized),
        string description = "Unauthorized error"
    ) => new(code, description, ErrorKind.Unauthorized);

    public static Error Forbidden(
        string code = nameof(Forbidden),
        string description = "Forbidden error"
    ) => new(code, description, ErrorKind.Forbidden);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Common\Results\ErrorKind.cs =======

namespace MechanicShop.Domain.Common.Results;

public enum ErrorKind
{
    Failure,
    Unexpected,
    Validation,
    Conflict,
    NotFound,
    Unauthorized,
    Forbidden,
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Common\Results\Result.cs =======

using System.ComponentModel;
using System.Text.Json.Serialization;
using MechanicShop.Domain.Common.Results.Abstraction;

namespace MechanicShop.Domain.Common.Results;

public readonly record struct Success;

public readonly record struct Updated;

public readonly record struct Deleted;

public readonly record struct Created;

public static class Result
{
    public static Success Success = default;
    public static Updated Updated = default;
    public static Deleted Deleted = default;
    public static Created Created = default;
}

public sealed class Result<TValue> : IResult<TValue>
{
    private readonly List<Error>? _errors = [];
    private readonly TValue? _value = default!;
    public bool IsSuccess { get; }
    public bool IsError => !IsSuccess;
    public List<Error> Errors => IsError ? _errors! : [];
    public TValue Value => IsSuccess ? _value! : default!;
    public Error TopError => (_errors!.Count > 0) ? _errors[0] : default;

    [JsonConstructor]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("For serializer only.", true)]
    public Result(TValue? value, List<Error>? errors, bool isSuccess)
    {
        if (isSuccess)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _errors = [];
            IsSuccess = true;
        }
        else
        {
            if (errors == null || errors.Count == 0)
            {
                throw new ArgumentException("Provide at least one error.", nameof(errors));
            }

            _errors = errors;
            _value = default!;
            IsSuccess = false;
        }
    }

    private Result(Error error)
    {
        _errors = [error];
        IsSuccess = false;
    }

    private Result(List<Error> errors)
    {
        if (errors is null || errors.Count == 0)
        {
            throw new ArgumentException(
                "Cannot create an ErrorOr<TValue> from an empty collection of errors. Provide at least one error.",
                nameof(errors)
            );
        }

        _errors = errors;

        IsSuccess = false;
    }

    private Result(TValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _value = value;

        IsSuccess = true;
    }

    public TValueNext Match<TValueNext>(
        Func<TValue, TValueNext> onValue,
        Func<List<Error>, TValueNext> onError
    ) => IsSuccess ? onValue(Value!) : onError(Errors);

    public static implicit operator Result<TValue>(Error error) => new(error);

    public static implicit operator Result<TValue>(List<Error> errors) => new(errors);

    public static implicit operator Result<TValue>(TValue value) => new(value);
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Common\Results\Abstraction\IResult.cs =======

namespace MechanicShop.Domain.Common.Results.Abstraction;

public interface IResult
{
    List<Error>? Errors { get; }

    bool IsSuccess { get; }
}

public interface IResult<out TValue> : IResult
{
    TValue Value { get; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Customers\Customer.cs =======

using System.Net.Mail;
using System.Text.RegularExpressions;
using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Customers;

public sealed class Customer : AuditableEntity
{
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string PhoneNumber { get; private set; }
    public string Address { get; private set; }
    private List<Vehicle> _vehicles = [];
    public IReadOnlyList<Vehicle> Vehicles => _vehicles.AsReadOnly();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Customer() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private Customer(
        Guid id,
        string name,
        string email,
        string phoneNumber,
        string address,
        List<Vehicle> vehicles
    )
        : base(id)
    {
        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
        Address = address;
        _vehicles = [.. vehicles];
    }

    public static Result<Customer> Create(
        Guid id,
        string name,
        string email,
        string phoneNumber,
        string address,
        List<Vehicle> vehicles
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomerErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber) || !Regex.IsMatch(phoneNumber, @"^\+?\d{7,15}$"))
        {
            return CustomerErrors.InvalidPhoneNumber;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return CustomerErrors.EmailRequired;
        }
        if (string.IsNullOrWhiteSpace(address))
        {
            return CustomerErrors.AddressRequired;
        }

        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            return CustomerErrors.EmailInvalid;
        }

        return new Customer(id, name, email, phoneNumber, address, vehicles);
    }

    public Result<Updated> AddVehicle(Vehicle vehicle)
    {
        _vehicles.Add(vehicle);
        return Result.Updated;
    }

    public Result<Updated> Update(string name, string phoneNumber, string email, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomerErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return CustomerErrors.EmailRequired;
        }
        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            return CustomerErrors.EmailInvalid;
        }
        if (string.IsNullOrWhiteSpace(phoneNumber) || !Regex.IsMatch(phoneNumber, @"^\+?\d{7,15}$"))
        {
            return CustomerErrors.InvalidPhoneNumber;
        }
        if (string.IsNullOrWhiteSpace(address))
        {
            return CustomerErrors.AddressRequired;
        }
        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
        Address = address;
        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Customers\CustomerErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Customers;

public static class CustomerErrors
{
    public static Error NameRequired =>
        Error.Validation("Customer_Name_Required", "Customer name is required");

    public static Error PhoneNumberRequired =>
        Error.Validation("Customer_Number_Required", "Phone number is required");

    public static Error EmailRequired =>
        Error.Validation("Customer_Email_Required", "Email is required");
    public static Error AddressRequired =>
        Error.Validation("Customer_Address_Required", "Address is required");

    public static Error EmailInvalid =>
        Error.Validation("Customer_Email_Invalid", "Email is invalid");

    public static Error EmailAlreadyInUse =>
        Error.Conflict("Customer_Email_Exists", "A customer with this email already exists.");

    public static readonly Error InvalidPhoneNumber = Error.Conflict(
        "Customer.InvalidPhoneNumber",
        "Phone number must be 7â€“15 digits and may start with '+'."
    );

    public static readonly Error CannotDeleteCustomerWithWorkOrders = Error.Conflict(
        "Customer.CannotDelete",
        "Customer cannot be deleted due to existing work orders."
    );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Customers\Vehicles\Vehicle.cs =======

using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;

namespace MechanicShop.Domain.Customers;

public sealed class Vehicle : AuditableEntity
{
    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public string Make { get; private set; }
    public string Model { get; private set; }
    public int Year { get; private set; }
    public string LicensePlate { get; private set; }
    public string VehicleInfo => $"{Make} | {Model} | {Year}";

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Vehicle() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private Vehicle(Guid id, string make, string model, int year, string licensePlate)
        : base(id)
    {
        Make = make;
        Model = model;
        Year = year;
        LicensePlate = licensePlate;
    }

    public static Result<Vehicle> Create(
        Guid id,
        string make,
        string model,
        int year,
        string licensePlate
    )
    {
        if (string.IsNullOrWhiteSpace(make))
        {
            return VehicleErrors.MakeRequired;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return VehicleErrors.ModelRequired;
        }

        if (string.IsNullOrWhiteSpace(licensePlate))
        {
            return VehicleErrors.LicensePlateRequired;
        }

        if (year < 1886 || year > DateTime.UtcNow.Year)
        {
            return VehicleErrors.YearInvalid;
        }

        return new Vehicle(id, make, model, year, licensePlate);
    }

    public Result<Updated> Update(string make, string model, int year, string licensePlate)
    {
        if (string.IsNullOrWhiteSpace(make))
        {
            return VehicleErrors.MakeRequired;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return VehicleErrors.ModelRequired;
        }

        if (year < 1886 || year > DateTime.UtcNow.Year)
        {
            return VehicleErrors.YearInvalid;
        }

        if (string.IsNullOrWhiteSpace(licensePlate))
        {
            return VehicleErrors.LicensePlateRequired;
        }

        Make = make;
        Model = model;
        Year = year;
        LicensePlate = licensePlate;

        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Customers\Vehicles\VehicleErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Customers.Vehicles;

public static class VehicleErrors
{
    public static Error MakeRequired =>
        Error.Validation("Vehicle_Make_Required", "Vehicle make is required");

    public static Error ModelRequired =>
        Error.Validation("Vehicle_Model_Required", "Vehicle model is required");

    public static Error LicensePlateRequired =>
        Error.Validation("Vehicle_LicensePlate_Make_Required", "Vehicle license plate is required");

    public static Error YearInvalid =>
        Error.Validation("Vehicle_Year_Invalid", "Year must be between 1886 and next year.");

    public static readonly Error CannotDeleteVehicleWithWorkOrders = Error.Conflict(
        "Vehicle.CannotDelete",
        "Vehicle cannot be deleted due to existing work orders."
    );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Employees\Employee.cs =======

using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;

public sealed class Employee : AuditableEntity
{
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string FullName => $"{FirstName} {LastName}";
    public Role Role { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Employee() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private Employee(Guid id, string firstName, string lastName, Role role)
        : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Role = role;
    }

    public static Result<Employee> Create(Guid id, string firstName, string lastName, Role role)
    {
        if (id == Guid.Empty)
            return EmployeeErrors.IdRequired;
        if (string.IsNullOrWhiteSpace(firstName))
            return EmployeeErrors.FirstNameRequired;
        if (string.IsNullOrWhiteSpace(lastName))
            return EmployeeErrors.LastNameRequired;
        if (!Enum.IsDefined(role))
            return EmployeeErrors.RoleInvalid;

        return new Employee(id, firstName.Trim(), lastName.Trim(), role);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Employees\EmployeeErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Employees;

public static class EmployeeErrors
{
    public static readonly Error IdRequired = Error.Validation(
        "Employee.Id.Required",
        "Employee Id is required."
    );

    public static Error FirstNameRequired =>
        Error.Validation("Employee.FirstName.Required", "First name is required.");

    public static Error LastNameRequired =>
        Error.Validation("Employee.LastName.Required", "Last name is required.");

    public static Error RoleInvalid =>
        Error.Validation("Employee.Role.Invalid", "Invalid role assigned to employee.");
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Identity\RefreshToken.cs =======

using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Identity;

public sealed class RefreshToken : AuditableEntity
{
    public string? Token { get; private set; }
    public string? UserId { get; private set; }
    public DateTimeOffset ExpiresOnUtc { get; private set; }

    private RefreshToken() { }

    private RefreshToken(Guid id, string token, string userId, DateTimeOffset expiresOnUtc)
        : base(id)
    {
        Token = token;
        UserId = userId;
        ExpiresOnUtc = expiresOnUtc;
    }

    public static Result<RefreshToken> Create(
        Guid id,
        string? token,
        string? userId,
        DateTimeOffset expiresOnUtc
    )
    {
        if (id == Guid.Empty)
        {
            return RefreshTokenErrors.IdRequired;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return RefreshTokenErrors.TokenRequired;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return RefreshTokenErrors.UserIdRequired;
        }

        if (expiresOnUtc <= DateTimeOffset.UtcNow)
        {
            return RefreshTokenErrors.ExpiryInvalid;
        }

        return new RefreshToken(id, token, userId, expiresOnUtc);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Identity\RefreshTokenErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Identity;

public static class RefreshTokenErrors
{
    public static readonly Error IdRequired = Error.Validation(
        "RefreshToken_Id_Required",
        "Refresh token ID is required."
    );

    public static readonly Error TokenRequired = Error.Validation(
        "RefreshToken_Token_Required",
        "Token value is required."
    );

    public static readonly Error UserIdRequired = Error.Validation(
        "RefreshToken_UserId_Required",
        "User ID is required."
    );

    public static readonly Error ExpiryInvalid = Error.Validation(
        "RefreshToken_Expiry_Invalid",
        "Expiry must be in the future."
    );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Identity\Role.cs =======

namespace MechanicShop.Domain.Identity;

public enum Role
{
    Manager,
    Labor,
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Invoices\Invoice.cs =======

using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Domain.Invoices;

public sealed class Invoice : AuditableEntity
{
    public Guid WorkOrderId { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Unpaid;
    private readonly List<InvoiceLineItem> _lineItems = [];
    public IReadOnlyList<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();
    public decimal Subtotal => LineItems.Sum(x => x.LineTotal);
    public decimal TaxRate { get; private set; }
    public decimal? DiscountAmount { get; private set; }
    public WorkOrder? WorkOrder { get; private set; }
    public decimal SubtotalAfterDiscount => Subtotal - (DiscountAmount ?? 0m);
    public decimal TaxAmount => SubtotalAfterDiscount * TaxRate;
    public decimal Total => SubtotalAfterDiscount + TaxAmount;
    public decimal ActualLaborCost { get; private set; }
    public decimal ActualPartsCost { get; private set; }

    private Invoice() { }

    private Invoice(
        Guid id,
        Guid workOrderId,
        DateTimeOffset issuedAt,
        List<InvoiceLineItem> lineItems,
        decimal discountAmount,
        decimal taxRate,
        decimal laborCost,
        decimal partsCost
    )
        : base(id)
    {
        WorkOrderId = workOrderId;
        IssuedAtUtc = issuedAt;
        DiscountAmount = discountAmount;
        Status = InvoiceStatus.Unpaid;
        TaxRate = taxRate;
        _lineItems = [.. lineItems];
        ActualLaborCost = laborCost;
        ActualPartsCost = partsCost;
    }

    public static Result<Invoice> Create(
        Guid id,
        Guid workOrderId,
        List<InvoiceLineItem> items,
        decimal discountAmount,
        decimal taxRate,
        TimeProvider datetime,
        decimal laborCost,
        decimal partsCost
    )
    {
        if (workOrderId == Guid.Empty)
        {
            return InvoiceErrors.WorkOrderIdInvalid;
        }

        if (items is null || items.Count == 0)
        {
            return InvoiceErrors.LineItemsEmpty;
        }
        if (taxRate < 0)
        {
            return InvoiceErrors.TaxRateNegative;
        }
        if (laborCost < 0 || partsCost < 0)
        {
            return InvoiceErrors.ActualCostsNegative;
        }

        var invoice = new Invoice(
            id,
            workOrderId,
            datetime.GetUtcNow(),
            items,
            0m,
            taxRate,
            laborCost,
            partsCost
        );
        var result = invoice.ApplyDiscount(discountAmount);
        if (result.IsError)
        {
            return result.Errors;
        }

        return invoice;
    }

    public Result<Updated> ApplyDiscount(decimal discountAmount)
    {
        if (Status != InvoiceStatus.Unpaid)
        {
            return InvoiceErrors.InvoiceLocked;
        }

        if (discountAmount < 0)
        {
            return InvoiceErrors.DiscountNegative;
        }

        if (discountAmount > Subtotal)
        {
            return InvoiceErrors.DiscountExceedsSubtotal;
        }

        DiscountAmount = discountAmount;

        return Result.Updated;
    }

    public Result<Updated> MarkAsPaid(TimeProvider timeProvider)
    {
        if (Status != InvoiceStatus.Unpaid)
        {
            return InvoiceErrors.InvoiceLocked;
        }

        Status = InvoiceStatus.Paid;
        PaidAtUtc = timeProvider.GetUtcNow();

        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Invoices\InvoiceErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Invoices;

public static class InvoiceErrors
{
    public static readonly Error WorkOrderIdInvalid = Error.Validation(
        code: "Invoice.WorkOrderId.Invalid",
        description: "WorkOrderId is invalid"
    );

    public static readonly Error LineItemsEmpty = Error.Validation(
        code: "Invoice.LineItems.Empty",
        description: "Invoice must have line items"
    );

    public static readonly Error InvoiceLocked = Error.Validation(
        code: "Invoice.Locked",
        description: "Invoice is locked"
    );

    public static readonly Error DiscountNegative = Error.Validation(
        code: "Invoice.Discount.Negative",
        description: "Discount cannot be negative"
    );

    public static readonly Error TaxRateNegative = Error.Validation(
        code: "Invoice.TaxRate.Negative",
        description: "TaxRate cannot be negative"
    );

    public static readonly Error DiscountExceedsSubtotal = Error.Validation(
        code: "Invoice.Discount.ExceedsSubtotal",
        description: "Discount exceeds subtotal"
    );

    public static readonly Error ActualCostsNegative = Error.Validation(
        code: "Invoice.costs.Negative",
        description: "Actual labor and parts costs cannot be negative"
    );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Invoices\Enums\InvoiceStatus.cs =======

namespace MechanicShop.Domain.Invoices;

public enum InvoiceStatus
{
    Unpaid = 0,
    Paid = 1,
    Refunded = 2,
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Invoices\InvoiceLineItems\InvoiceLineItem.cs =======

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices.InvoiceLineItems;

namespace MechanicShop.Domain.Invoices;

public sealed class InvoiceLineItem
{
    public Guid InvoiceId { get; private set; }
    public int LineNumber { get; private set; }
    public string Description { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => Quantity * UnitPrice;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private InvoiceLineItem() { }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private InvoiceLineItem(
        Guid invoiceId,
        int lineNumber,
        string description,
        int quantity,
        decimal unitPrice
    )
    {
        InvoiceId = invoiceId;
        LineNumber = lineNumber;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public static Result<InvoiceLineItem> Create(
        Guid invoiceId,
        int lineNumber,
        string description,
        int quantity,
        decimal unitPrice
    )
    {
        if (invoiceId == Guid.Empty)
        {
            return InvoiceLineItemErrors.InvoiceIdRequired;
        }

        if (lineNumber <= 0)
        {
            return InvoiceLineItemErrors.LineNumberInvalid;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return InvoiceLineItemErrors.DescriptionRequired;
        }
        if (description.Length > 500)
        {
            return InvoiceLineItemErrors.DescriptionTooLong;
        }

        if (quantity <= 0)
        {
            return InvoiceLineItemErrors.QuantityInvalid;
        }

        if (unitPrice <= 0)
        {
            return InvoiceLineItemErrors.UnitPriceInvalid;
        }

        return new InvoiceLineItem(invoiceId, lineNumber, description.Trim(), quantity, unitPrice);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\Invoices\InvoiceLineItems\InvoiceLineItemErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Invoices.InvoiceLineItems;

public static class InvoiceLineItemErrors
{
    public static Error InvoiceIdRequired =>
        Error.Validation(
            code: "InvoiceLineItemErrors.InvoiceIdRequired",
            description: "InvoiceId is required."
        );

    public static Error LineNumberInvalid =>
        Error.Validation(
            code: "InvoiceLineItemErrors.LineNumberInvalid",
            description: "Line number must be greater than 0."
        );

    public static Error DescriptionRequired =>
        Error.Validation(
            code: "InvoiceLineItemErrors.DescriptionRequired",
            description: "Description is required."
        );

    public static Error DescriptionTooLong =>
        Error.Validation(
            code: "InvoiceLineItemErrors.DescriptionTooLong",
            description: "Description is too long."
        );

    public static Error QuantityInvalid =>
        Error.Validation(
            code: "InvoiceLineItemErrors.QuantityInvalid",
            description: "Quantity must be greater than 0."
        );

    public static Error UnitPriceInvalid =>
        Error.Validation(
            code: "InvoiceLineItemErrors.UnitPriceInvalid",
            description: "Unit price must be greater than 0."
        );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\RepairTasks\RepairTask.cs =======

using System.Security.Cryptography.X509Certificates;
using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Enums;

namespace MechanicShop.Domain.RepairTasks;

public sealed class RepairTask : AuditableEntity
{
    public string Name { get; private set; }
    public RepairDurationInMinutes EstimatedDurationInMins { get; private set; }
    public decimal LaborCost { get; private set; }
    private readonly List<Part> _parts = [];
    public IEnumerable<Part> Parts => _parts.AsReadOnly();
    public decimal TotalCost => LaborCost + Parts.Sum(p => p.Cost * p.Quantity);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private RepairTask() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private RepairTask(
        Guid id,
        string name,
        RepairDurationInMinutes estimatedDurationInMins,
        decimal laborCost,
        List<Part> parts
    )
        : base(id)
    {
        Name = name;
        EstimatedDurationInMins = estimatedDurationInMins;
        LaborCost = laborCost;
        _parts = parts;
    }

    public static Result<RepairTask> Create(
        Guid id,
        string name,
        RepairDurationInMinutes estimatedDurationInMins,
        decimal laborCost,
        List<Part> parts
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return RepairTaskErrors.NameRequired;
        }

        if (!Enum.IsDefined(typeof(RepairDurationInMinutes), estimatedDurationInMins))
        {
            return RepairTaskErrors.DurationInvalid;
        }

        if (laborCost <= 0)
        {
            return RepairTaskErrors.LaborCostInvalid;
        }
        return new RepairTask(id, name.Trim(), estimatedDurationInMins, laborCost, parts);
    }

    public Result<Updated> Update(
        string name,
        RepairDurationInMinutes estimatedDurationInMins,
        decimal laborCost
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return RepairTaskErrors.NameRequired;
        }

        if (!Enum.IsDefined(estimatedDurationInMins))
        {
            return RepairTaskErrors.DurationInvalid;
        }

        if (laborCost <= 0)
        {
            return RepairTaskErrors.LaborCostInvalid;
        }
        Name = name.Trim();
        EstimatedDurationInMins = estimatedDurationInMins;
        LaborCost = laborCost;
        return Result.Updated;
    }

    public Result<Updated> UpsertParts(List<Part> incomingParts)
    {
        _parts.RemoveAll(existing => incomingParts.All(p => existing.Id != p.Id));

        foreach (var incoming in incomingParts)
        {
            var existing = _parts.FirstOrDefault(p => p.Id == incoming.Id);
            if (existing is null)
            {
                _parts.Add(incoming);
            }
            else
            {
                var updatePartResult = existing.Update(
                    incoming.Name,
                    incoming.Cost,
                    incoming.Quantity
                );
                if (updatePartResult.IsError)
                {
                    return updatePartResult.Errors;
                }
            }
        }
        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\RepairTasks\RepairTaskErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.RepairTasks;

public static class RepairTaskErrors
{
    public static Error NameRequired =>
        Error.Validation("RepairTask.Name.Required", "Name is required.");

    public static Error LaborCostInvalid =>
        Error.Validation(
            "RepairTask.LaborCost.Invalid",
            "Labor cost must be between 1 and 10,000."
        );

    public static Error DurationInvalid =>
        Error.Validation("RepairTask.Duration.Invalid", "Invalid duration selected.");

    public static Error PartsRequired =>
        Error.Validation("RepairTask.Parts.Required", "At least one part is required.");

    public static Error PartNameRequired =>
        Error.Validation("RepairTask.Parts.Name.Required", "All parts must have a name.");

    public static Error AtLeastOneRepairTaskIsRequired =>
        Error.Validation(
            code: "RepairTask.Required",
            description: "At least one repair task must be specified."
        );

    public static Error InUse =>
        Error.Conflict(
            "RepairTask.InUse",
            "Cannot delete a repair task that is used in work orders."
        );

    public static Error DuplicateName =>
        Error.Conflict(
            "RepairTaskPart.Duplicate",
            "A part with the same name already exists in this repair task."
        );

    public static readonly Error NameAlreadyExists = Error.Conflict(
        "RepairTask.Name.Exists",
        "A repair task with this name already exists in the catalog."
    );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\RepairTasks\Enums\RepairDurationInMinutes.cs =======

namespace MechanicShop.Domain.RepairTasks.Enums;

public enum RepairDurationInMinutes
{
    Min15 = 15,
    Min30 = 30,
    Min45 = 45,
    Min60 = 60,
    Min75 = 75,
    Min90 = 90,
    Min105 = 105,
    Min120 = 120,
    Min135 = 135,
    Min150 = 150,
    Min165 = 165,
    Min180 = 180,
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\RepairTasks\Parts\Part.cs =======

using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Parts;

namespace MechanicShop.Domain.RepairTasks;

public sealed class Part : AuditableEntity
{
    public string Name { get; private set; }

    public decimal Cost { get; private set; }

    public int Quantity { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Part() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private Part(Guid id, string name, decimal cost, int quantity)
        : base(id)
    {
        Name = name;
        Cost = cost;
        Quantity = quantity;
    }

    public static Result<Part> Create(Guid id, string name, decimal cost, int quantity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return PartErrors.NameRequired;
        }

        if (cost <= 0 || cost > 10000)
        {
            return PartErrors.CostInvalid;
        }

        if (quantity <= 0 || quantity > 10)
        {
            return PartErrors.QuantityInvalid;
        }

        return new Part(id, name.Trim(), cost, quantity);
    }

    public Result<Updated> Update(string name, decimal cost, int quantity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return PartErrors.NameRequired;
        }

        if (cost <= 0 || cost > 10000)
        {
            return PartErrors.CostInvalid;
        }

        if (quantity <= 0 || quantity > 10)
        {
            return PartErrors.QuantityInvalid;
        }

        Name = name.Trim();
        Cost = cost;
        Quantity = quantity;

        return Result.Updated;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\RepairTasks\Parts\PartErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.RepairTasks.Parts;

public static class PartErrors
{
    public static readonly Error NameRequired = Error.Validation(
        "Part.Name.Required",
        "Part name is required."
    );

    public static readonly Error CostInvalid = Error.Validation(
        "Part.Cost.Invalid",
        "Part cost must be between 1 and 10,000."
    );

    public static readonly Error QuantityInvalid = Error.Validation(
        "Part.Quantity.Invalid",
        "Quantity must be between 1 and 10."
    );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\WorkOrder.cs =======

using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders.Events;

namespace MechanicShop.Domain.WorkOrders;

public sealed class WorkOrder : AuditableEntity
{
    public Guid VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }
    public Guid LaborId { get; private set; }
    public Employee? Labor { get; private set; }
    public Invoice? Invoice { get; private set; }
    public Spot Spot { get; private set; }
    public DateTimeOffset StartAtUtc { get; private set; }
    public DateTimeOffset EndAtUtc { get; private set; }
    public WorkOrderState State { get; private set; } = WorkOrderState.Scheduled;
    private readonly List<RepairTask> _repairTasks = [];
    public IReadOnlyList<RepairTask> RepairTasks => _repairTasks.AsReadOnly();
    public bool IsEditable => State == WorkOrderState.Scheduled;
    public decimal? TotalPartsCost =>
        _repairTasks.SelectMany(rt => rt.Parts).Sum(p => p.Cost * p.Quantity);
    public decimal? TotalLaborCost => _repairTasks.Sum(rt => rt.LaborCost);
    public decimal? Total => (TotalPartsCost ?? 0) + (TotalLaborCost ?? 0);

    private WorkOrder() { }

    private WorkOrder(
        Guid id,
        Guid vehicleId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid laborId,
        Spot spot,
        List<RepairTask> repairTasks
    )
        : base(id)
    {
        VehicleId = vehicleId;
        StartAtUtc = startAt;
        EndAtUtc = endAt;
        LaborId = laborId;
        Spot = spot;
        _repairTasks = repairTasks;
    }

    public static Result<WorkOrder> Create(
        Guid id,
        Guid vehicleId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid laborId,
        Spot spot,
        List<RepairTask> repairTasks,
        TimeProvider timeProvider
    )
    {
        if (id == Guid.Empty)
        {
            return WorkOrderErrors.WorkOrderIdRequired;
        }
        if (vehicleId == Guid.Empty)
        {
            return WorkOrderErrors.VehicleIdRequired;
        }
        if (laborId == Guid.Empty)
        {
            return WorkOrderErrors.LaborIdRequired;
        }

        if (repairTasks == null || repairTasks.Count == 0)
        {
            return WorkOrderErrors.RepairTasksRequired;
        }
        if (startAt < timeProvider.GetUtcNow())
        {
            return WorkOrderErrors.StartDateInPast;
        }

        if (endAt <= startAt)
        {
            return WorkOrderErrors.InvalidEndDate;
        }
        if (!Enum.IsDefined(typeof(Spot), spot))
        {
            return WorkOrderErrors.SpotInvalid;
        }
        var workOrder = new WorkOrder(id, vehicleId, startAt, endAt, laborId, spot, repairTasks);
        workOrder.AddDomainEvent(new WorkOrderCreated { WorkOrderId = id });
        return workOrder;
    }

    public Result<Updated> UpdateRepairTasks(List<RepairTask> newTasks)
    {
        if (!IsEditable)
        {
            return WorkOrderErrors.Readonly;
        }

        if (newTasks is null || newTasks.Count == 0)
        {
            return WorkOrderErrors.RepairTasksRequired;
        }

        _repairTasks.Clear();
        foreach (var task in newTasks)
        {
            if (!_repairTasks.Any(x => x.Id == task.Id))
            {
                _repairTasks.Add(task);
            }
        }

        AddDomainEvent(new WorkOrderRepairTasksUpdated { WorkOrderId = Id });

        return Result.Updated;
    }

    public Result<Updated> UpdateTiming(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TimeProvider timeProvider
    )
    {
        if (!IsEditable)
        {
            return WorkOrderErrors.TimingReadonly(Id.ToString(), State);
        }
        if (startTime < timeProvider.GetUtcNow())
        {
            return WorkOrderErrors.StartDateInPast;
        }
        if (endTime <= startTime)
        {
            return WorkOrderErrors.InvalidEndDate;
        }

        StartAtUtc = startTime;
        EndAtUtc = endTime;
        AddDomainEvent(new WorkOrderRescheduled() { WorkOrderId = Id });
        return Result.Updated;
    }

    public Result<Updated> UpdateLabor(Guid laborId)
    {
        if (!IsEditable)
        {
            return WorkOrderErrors.Readonly;
        }

        if (laborId == Guid.Empty)
        {
            return WorkOrderErrors.LaborIdEmpty(Id.ToString());
        }

        LaborId = laborId;
        AddDomainEvent(new WorkOrderLaborReassigned() { WorkOrderId = Id });
        return Result.Updated;
    }

    public bool CanTransitionTo(WorkOrderState newState)
    {
        return (State, newState) switch
        {
            (WorkOrderState.Scheduled, WorkOrderState.InProgress) => true,
            (WorkOrderState.InProgress, WorkOrderState.Completed) => true,
            (_, WorkOrderState.Cancelled) when State != WorkOrderState.Completed => true,
            _ => false,
        };
    }

    public Result<Updated> UpdateState(WorkOrderState newState)
    {
        if (!CanTransitionTo(newState))
        {
            return WorkOrderErrors.InvalidStateTransition(State, newState);
        }

        State = newState;

        if (State == WorkOrderState.Completed)
        {
            AddDomainEvent(new WorkOrderCompleted { WorkOrderId = Id });
        }

        return Result.Updated;
    }

    public Result<Updated> Cancel()
    {
        if (!CanTransitionTo(WorkOrderState.Cancelled))
        {
            return WorkOrderErrors.InvalidStateTransition(State, WorkOrderState.Cancelled);
        }

        State = WorkOrderState.Cancelled;
        AddDomainEvent(new WorkOrderCancelled { WorkOrderId = Id });
        return Result.Updated;
    }

    public Result<Updated> UpdateSpot(Spot newSpot)
    {
        if (!IsEditable)
        {
            return WorkOrderErrors.Readonly;
        }

        if (!Enum.IsDefined(typeof(Spot), newSpot))
        {
            return WorkOrderErrors.SpotInvalid;
        }

        Spot = newSpot;
        AddDomainEvent(new WorkOrderSpotUpdated() { WorkOrderId = Id });
        return Result.Updated;
    }

    public Result<Deleted> Delete()
    {
        if (State is not WorkOrderState.Scheduled)
        {
            return WorkOrderErrors.Readonly;
        }

        AddDomainEvent(new WorkOrderRemoved { WorkOrderId = Id });

        return Result.Deleted;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\WorkOrderErrors.cs =======

using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.WorkOrders;

public static class WorkOrderErrors
{
    public static Error WorkOrderIdRequired =>
        Error.Validation(
            code: "WorkOrderErrors.WorkOrderIdRequired",
            description: "WorkOrder Id is required"
        );

    public static Error VehicleIdRequired =>
        Error.Validation(
            code: "WorkOrderErrors.VehicleIdRequired",
            description: "Vehicle Id is required"
        );

    public static Error LaborIdRequired =>
        Error.Validation(
            code: "WorkOrderErrors.LaborIdRequired",
            description: "Labor Id is required"
        );

    public static Error RepairTasksRequired =>
        Error.Validation(
            code: "WorkOrderErrors.RepairTasksRequired",
            description: "At least one repair task is required"
        );
    public static Error StartDateInPast =>
        Error.Validation(
            code: "WorkOrderErrors.StartDateInPast",
            description: "Scheduled start date cannot be in the past."
        );
    public static Error InvalidEndDate =>
        Error.Validation(
            code: "WorkOrderErrors.InvalidEndDate",
            description: "End time must be after start time."
        );

    public static Error SpotInvalid =>
        Error.Validation(
            code: "WorkOrderErrors.SpotInvalid",
            description: "The provided spot is invalid"
        );

    public static Error Readonly =>
        Error.Conflict(code: "WorkOrderErrors.Readonly", description: "WorkOrder is read-only.");
    public static Error RepairTaskAlreadyAdded =>
        Error.Conflict(
            code: "WorkOrderErrors.RepairTaskAlreadyAdded",
            description: "Repair task already exists."
        );

    public static Error TimingReadonly(string id, WorkOrderState state) =>
        Error.Conflict(
            code: "WorkOrderErrors.TimingReadonly",
            description: $"WorkOrder '{id}': Can't Modify timing when WorkOrder status is '{state}'."
        );

    public static Error LaborIdEmpty(string id) =>
        Error.Validation(
            code: "WorkOrderErrors.LaborIdEmpty",
            description: $"WorkOrder '{id}': Labor Id is empty"
        );

    public static Error StateTransitionNotAllowed(DateTimeOffset startAtUtc) =>
        Error.Conflict(
            code: "WorkOrderErrors.StateTransitionNotAllowed",
            description: $"State transition is not allowed before the work orders scheduled start time {startAtUtc:yyyy-MM-dd HH:mm} UTC."
        );

    public static Error InvalidStateTransition(WorkOrderState current, WorkOrderState next) =>
        Error.Conflict(
            code: "WorkOrderErrors.InvalidStateTransition",
            description: $"WorkOrder Invalid State transition from '{current}' to '{next}'."
        );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Enums\Spot.cs =======

namespace MechanicShop.Domain.WorkOrders;

public enum Spot
{
    A,
    B,
    C,
    D,
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Enums\WorkOrderState.cs =======

namespace MechanicShop.Domain.WorkOrders;

public enum WorkOrderState
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Events\WorkOrderCancelled.cs =======

using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderCancelled : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Events\WorkOrderCompleted.cs =======

using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderCompleted : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Events\WorkOrderCreated.cs =======

using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderCreated : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Events\WorkOrderLaborReassigned.cs =======

using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderLaborReassigned : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Events\WorkOrderRemoved.cs =======

using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderRemoved : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Events\WorkOrderRepairTasksUpdated.cs =======

using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderRepairTasksUpdated : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Events\WorkOrderRescheduled.cs =======

using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderRescheduled : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Domain\Entities\WorkOrders\Events\WorkOrderSpotUpdated.cs =======

using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderSpotUpdated : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\DependencyInjection.cs =======

using System.Text;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Identity;
using MechanicShop.Infrastructure.BackgroundJobs;
using MechanicShop.Infrastructure.Data;
using MechanicShop.Infrastructure.Data.Interceptors;
using MechanicShop.Infrastructure.Identity;
using MechanicShop.Infrastructure.RealTime;
using MechanicShop.Infrastructure.Services;
using MechanicShop.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton(TimeProvider.System);

        var appSettings =
            configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        ArgumentNullException.ThrowIfNull(connectionString);

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>(
            (sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                options.UseSqlServer(
                    connectionString,
                    sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null
                        );
                    }
                );
            }
        );

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<AppDbContextInitializer>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var jwtSettings = configuration.GetSection("JwtSettings");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings["Secret"]!)
                    ),
                };
            });

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequiredUniqueChars = 1;
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IAuthorizationHandler, LaborAssignedHandler>();

        services
            .AddAuthorizationBuilder()
            .AddPolicy("ManagerOnly", policy => policy.RequireRole(nameof(Role.Manager)))
            .AddPolicy(
                "SelfScopedWorkOrderAccess",
                policy => policy.Requirements.Add(new LaborAssignedRequirement())
            );

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            options.InstanceName = "MechanicShop:";
        });

        services.AddHybridCache(options =>
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(appSettings.DistributedCacheExpirationMins),
                LocalCacheExpiration = TimeSpan.FromMinutes(appSettings.LocalCacheExpirationInMins),
            }
        );

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IWorkOrderPolicy, WorkOrderPolicy>();
        services.AddScoped<ITokenProvider, TokenProvider>();
        services.AddScoped<IInvoicePdfGenerator, InvoicePdfGenerator>();
        services.AddScoped<IWorkOrderNotifier, SignalRWorkOrderNotifier>();

        services.AddHostedService<OverdueBookingCleanupService>();

        return services;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\BackgroundJobs\OverdueBookingCleanupService.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MechanicShop.Infrastructure.BackgroundJobs;

public class OverdueBookingCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<OverdueBookingCleanupService> logger,
    IOptions<AppSettings> options,
    TimeProvider dateTime
) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<OverdueBookingCleanupService> _logger = logger;
    private readonly TimeProvider _dateTime = dateTime;
    private readonly AppSettings _appSettings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(_appSettings.OverdueBookingCleanupFrequencyMinutes)
        );

        while (await timer.WaitForNextTickAsync(ct))
        {
            _logger.LogInformation("Checking overdue work orders at {Now}", _dateTime.GetUtcNow());

            try
            {
                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var cutOff = _dateTime
                    .GetUtcNow()
                    .AddMinutes(-_appSettings.BookingCancellationThresholdMinutes);

                var overdue = await db
                    .WorkOrders.Where(w =>
                        w.State == WorkOrderState.Scheduled && w.StartAtUtc <= cutOff
                    )
                    .ToListAsync();

                if (overdue.Count > 0)
                {
                    foreach (var wo in overdue)
                    {
                        var result = wo.Cancel();

                        if (result.IsError)
                        {
                            _logger.LogWarning(
                                "Failed to cancel WorkOrder {Id}: {Error}",
                                wo.Id,
                                result.Errors
                            );
                        }
                    }
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation(
                        "Cancelled {Count} overdue work orders: {Ids}",
                        overdue.Count,
                        overdue.Select(w => w.Id)
                    );
                }
                else
                {
                    _logger.LogInformation("No overdue work orders found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up overdue work orders.");
            }
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\AppDbContext.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IPublisher publisher)
    : IdentityDbContext<AppUser>(options),
        IAppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Part> Parts => Set<Part>();

    public DbSet<RepairTask> RepairTasks => Set<RepairTask>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public override async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        var entities = ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();
        var events = entities.SelectMany(e => e.DomainEvents).ToList();

        var result = await base.SaveChangesAsync(ct);

        foreach (var domainEvent in events)
            await publisher.Publish(domainEvent, ct);

        foreach (var entity in entities)
            entity.ClearDomainEvents();
        return result;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\AppDbContextInitializer.cs =======

using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Infrastructure.Data;

public static class InitializerExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var initializer = scope.ServiceProvider.GetRequiredService<AppDbContextInitializer>();

        await initializer.InitializeAsync();

        await initializer.SeedAsync();
    }
}

public class AppDbContextInitializer(
    ILogger<AppDbContextInitializer> logger,
    AppDbContext context,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    TimeProvider timeProvider
)
{
    private readonly ILogger<AppDbContextInitializer> _logger = logger;
    private readonly AppDbContext _context = context;
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task InitializeAsync()
    {
        try
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        var managerRole = new IdentityRole(nameof(Role.Manager));

        if (_roleManager.Roles.All(r => r.Name != managerRole.Name))
        {
            await _roleManager.CreateAsync(managerRole);
        }

        var laborRole = new IdentityRole(nameof(Role.Labor));

        if (_roleManager.Roles.All(r => r.Name != laborRole.Name))
        {
            await _roleManager.CreateAsync(laborRole);
        }

        var manager = new AppUser
        {
            Id = "19a59129-6c20-417a-834d-11a208d32d96",
            Email = "pm@localhost",
            UserName = "pm@localhost",
            EmailConfirmed = true,
        };

        if (_userManager.Users.All(u => u.Email != manager.Email))
        {
            await _userManager.CreateAsync(manager, manager.Email);

            if (!string.IsNullOrWhiteSpace(managerRole.Name))
            {
                await _userManager.AddToRolesAsync(manager, [managerRole.Name]);
            }
        }

        var labor01 = new AppUser
        {
            Id = "b6327240-0aea-46fc-863a-777fc4e42560",
            Email = "john.labor@localhost",
            UserName = "john.labor@localhost",
            EmailConfirmed = true,
        };

        if (_userManager.Users.All(u => u.Email != labor01.Email))
        {
            await _userManager.CreateAsync(labor01, labor01.Email);

            if (!string.IsNullOrWhiteSpace(laborRole.Name))
            {
                await _userManager.AddToRolesAsync(labor01, [laborRole.Name]);
            }
        }

        var labor02 = new AppUser
        {
            Id = "8104ab20-26c2-4651-b1de-c0baf04dbbd9",
            Email = "peter.labor@localhost",
            UserName = "peter.labor@localhost",
            EmailConfirmed = true,
        };

        if (_userManager.Users.All(u => u.Email != labor02.Email))
        {
            await _userManager.CreateAsync(labor02, labor02.Email);

            if (!string.IsNullOrWhiteSpace(laborRole.Name))
            {
                await _userManager.AddToRolesAsync(labor02, [laborRole.Name]);
            }
        }

        var labor03 = new AppUser
        {
            Id = "e17c83de-1089-4f19-bf79-5f789133d37f",
            Email = "kevin.labor@localhost",
            UserName = "kevin.labor@localhost",
            EmailConfirmed = true,
        };

        if (_userManager.Users.All(u => u.Email != labor03.Email))
        {
            await _userManager.CreateAsync(labor03, labor03.Email);

            if (!string.IsNullOrWhiteSpace(laborRole.Name))
            {
                await _userManager.AddToRolesAsync(labor03, [laborRole.Name]);
            }
        }

        var labor04 = new AppUser
        {
            Id = "54cd01ba-b9ae-4c14-bab6-f3df0219ba4c",
            Email = "suzan.labor@localhost",
            UserName = "suzan.labor@localhost",
            EmailConfirmed = true,
        };

        if (_userManager.Users.All(u => u.Email != labor04.Email))
        {
            await _userManager.CreateAsync(labor04, labor04.Email);

            if (!string.IsNullOrWhiteSpace(laborRole.Name))
            {
                await _userManager.AddToRolesAsync(labor04, [laborRole.Name]);
            }
        }

        if (!_context.Employees.Any())
        {
            _context.Employees.AddRange([
                Employee.Create(Guid.Parse(manager.Id), "Primary", "Manager", Role.Manager).Value,
                Employee.Create(Guid.Parse(labor01.Id), "John", "S.", Role.Labor).Value,
                Employee.Create(Guid.Parse(labor02.Id), "Peter", "R.", Role.Labor).Value,
                Employee.Create(Guid.Parse(labor03.Id), "Kevin", "M.", Role.Labor).Value,
                Employee.Create(Guid.Parse(labor04.Id), "Suzan", "L.", Role.Labor).Value,
            ]);
        }

        if (!_context.Customers.Any())
        {
            List<Vehicle> vehiclesC1 =
            [
                Vehicle
                    .Create(
                        id: Guid.Parse("61401e63-007b-4b1c-8914-9eb6e9bd95c5"),
                        make: "Toyota",
                        model: "Camry",
                        year: 2020,
                        licensePlate: "ABC123"
                    )
                    .Value,
                Vehicle
                    .Create(
                        id: Guid.Parse("13c80914-41ad-4d46-b7bb-60f6c89ad01e"),
                        make: "Honda",
                        model: "Civic",
                        year: 2018,
                        licensePlate: "XYZ456"
                    )
                    .Value,
            ];

            List<Vehicle> vehiclesC2 =
            [
                Vehicle
                    .Create(
                        id: Guid.Parse("a04f329d-0f5a-46a0-beae-699c034ae401"),
                        make: "Ford",
                        model: "Focus",
                        year: 2021,
                        licensePlate: "DEF789"
                    )
                    .Value,
                Vehicle
                    .Create(
                        id: Guid.Parse("cf60e95b-5752-4c26-aa07-31a34164606c"),
                        make: "Chevrolet",
                        model: "Malibu",
                        year: 2019,
                        licensePlate: "GHI012"
                    )
                    .Value,
            ];

            _context.Customers.AddRange([
                Customer
                    .Create(
                        id: Guid.Parse("f522bbe5-e3b1-4e2c-a8a3-c41550dcf39d"),
                        name: "John Doe",
                        phoneNumber: "123456789",
                        email: "john.doe@localhost",
                        address: "123 Main St, AnyTown, USA",
                        vehicles: vehiclesC1
                    )
                    .Value,
                Customer
                    .Create(
                        id: Guid.Parse("73a04dd3-c81a-4a54-9882-ef1017eb192d"),
                        name: "Sarah Peter",
                        phoneNumber: "987654321",
                        email: "sarah.peter@localhost",
                        address: "456 Oak Ave, AnyTown, USA",
                        vehicles: vehiclesC2
                    )
                    .Value,
            ]);
        }

        if (!_context.RepairTasks.Any())
        {
            _context.RepairTasks.AddRange([
                RepairTask
                    .Create(
                        id: Guid.Parse("616aebb1-d515-4b40-8d47-8d5c0b67a313"),
                        name: "Engine Oil Change",
                        laborCost: 50.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min60,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("ec65225c-9066-4a1c-974f-f183c39fdd16"),
                                "Engine Oil",
                                25.00m,
                                1
                            ).Value,
                            Part.Create(
                                Guid.Parse("62ad80e3-2cff-41af-ab40-16fab8db8b38"),
                                "Oil Filter",
                                10.00m,
                                1
                            ).Value,
                        ]
                    )
                    .Value,
                RepairTask
                    .Create(
                        id: Guid.Parse("4fa0be55-06f6-4616-b086-e1f0c9354cd8"),
                        name: "Brake Replacement",
                        laborCost: 150.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min90,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("86375a12-715e-4aa4-aad9-c0f9ccf44a14"),
                                "Brake Pads",
                                40.00m,
                                2
                            ).Value,
                            Part.Create(
                                Guid.Parse("526d89c3-a971-4ea7-ba15-de6b50b13c21"),
                                "Brake Fluid",
                                15.00m,
                                1
                            ).Value,
                        ]
                    )
                    .Value,
                RepairTask
                    .Create(
                        id: Guid.Parse("a376b5d1-6b2d-4dd8-883e-d3d1721c1316"),
                        name: "Tire Rotation",
                        laborCost: 30.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min45,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("a46f974e-a198-4098-8a1f-6be6e68ec743"),
                                "Tire Valve",
                                5.00m,
                                4
                            ).Value,
                        ]
                    )
                    .Value,
                RepairTask
                    .Create(
                        id: Guid.Parse("a770cc6e-0c8b-4ac5-9ee6-6928682bd47e"),
                        name: "Battery Replacement",
                        laborCost: 70.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min30,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("d4fd3255-29dc-4d45-9d87-f58ab98bc28b"),
                                "Car Battery",
                                120.00m,
                                1
                            ).Value,
                        ]
                    )
                    .Value,
                RepairTask
                    .Create(
                        id: Guid.Parse("e4c2b675-4a60-488f-a7b4-61966e7e80e3"),
                        name: "Wheel Alignment",
                        laborCost: 80.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min60,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("fa3b9a7e-1c2d-4e3f-9b8a-0c1d2e3f4a5b"),
                                "Alignment Shim Kit (per wheel)",
                                5.00m,
                                4
                            ).Value,
                        ]
                    )
                    .Value,
                RepairTask
                    .Create(
                        id: Guid.Parse("1cb1608c-3bc7-4325-99c3-8244c0fb412f"),
                        name: "Air Conditioning Recharge",
                        laborCost: 100.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min30,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("526dca0a-d236-47d3-8e8f-c83d555b2de9"),
                                "Refrigerant",
                                50.00m,
                                1
                            ).Value,
                        ]
                    )
                    .Value,
                RepairTask
                    .Create(
                        id: Guid.Parse("a8e9b4e0-8581-40df-967d-51a0f4fabc0e"),
                        name: "Spark Plug Replacement",
                        laborCost: 90.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min60,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("019f5eab-a8a5-44d4-92b3-1f998e3f10c2"),
                                "Spark Plug",
                                10.00m,
                                4
                            ).Value,
                        ]
                    )
                    .Value,
                RepairTask
                    .Create(
                        id: Guid.Parse("90f2f3ef-3357-439e-9689-628aa08200c1"),
                        name: "Engine Diagnostic",
                        laborCost: 120.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min120,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f"),
                                "Smoke Leak Detector Fluid Cartridge",
                                20.00m,
                                1
                            ).Value,
                        ]
                    )
                    .Value,
                RepairTask
                    .Create(
                        id: Guid.Parse("d124651e-ca72-467e-ba28-81ea4a2080bc"),
                        name: "Timing Belt Replacement",
                        laborCost: 200.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min120,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("06b764a0-73a2-4c37-b279-adae3856499c"),
                                "Timing Belt",
                                75.00m,
                                1
                            ).Value,
                        ]
                    )
                    .Value,
                RepairTask
                    .Create(
                        id: Guid.Parse("cee9b309-8620-4028-8d38-2532771ab3ea"),
                        name: "Transmission Fluid Change",
                        laborCost: 100.00m,
                        estimatedDurationInMins: RepairDurationInMinutes.Min45,
                        parts:
                        [
                            Part.Create(
                                Guid.Parse("0a8b0c19-873a-4da0-811b-45ff85bca0ed"),
                                "Transmission Fluid",
                                60.00m,
                                1
                            ).Value,
                        ]
                    )
                    .Value,
            ]);
        }

        await _context.SaveChangesAsync(CancellationToken.None);

        if (!_context.WorkOrders.Any())
        {
            var repairTasks = _context.RepairTasks.ToList();
            var vehicles = _context.Vehicles.ToList();
            string[] labors = [labor01.Id, labor02.Id, labor03.Id, labor04.Id];
            Spot[] spots = [Spot.A, Spot.B, Spot.C, Spot.D];

            var generatedWorkOrders = new List<WorkOrder>();
            Random random = new();
            DateTimeOffset startDate = _timeProvider.GetUtcNow().Date.AddDays(1);
            DateTimeOffset endDate = startDate.AddMonths(1);
            TimeSpan openTime = TimeSpan.FromHours(12);
            TimeSpan closeTime = TimeSpan.FromHours(23);
            int totalMinutes = (int)(closeTime - openTime).TotalMinutes;

            while (startDate < endDate)
            {
                foreach (Spot spot in spots)
                {
                    int occupiedMinutes = 0;
                    int minOccupancy = (int)(totalMinutes * 0.6);
                    int maxOccupancy = (int)(totalMinutes * 0.8);
                    List<WorkOrder> spotWorkOrders = [];

                    DateTimeOffset currentTime = startDate.Add(openTime);

                    while (occupiedMinutes < minOccupancy && currentTime.TimeOfDay < closeTime)
                    {
                        var selectedTask = repairTasks
                            .OrderBy(_ => Guid.NewGuid())
                            .Take(
                                Random.Shared.Next(
                                    1,
                                    Math.Min(4, repairTasks.Select(t => t.Id).Distinct().Count())
                                )
                            )
                            .DistinctBy(t => t.Id)
                            .ToList();
                        var laborId = labors[random.Next(labors.Length)];
                        var duration = selectedTask.Sum(st => (int)st.EstimatedDurationInMins);

                        if (occupiedMinutes + duration > maxOccupancy)
                        {
                            break;
                        }

                        DateTimeOffset startAt = currentTime;
                        DateTimeOffset endAt = startAt.AddMinutes(duration);

                        var availableVehicle = vehicles
                            .Where(v =>
                                !generatedWorkOrders.Any(w =>
                                    w.VehicleId == v.Id
                                    && w.StartAtUtc.Date == startAt.Date
                                    && w.StartAtUtc < endAt
                                    && w.EndAtUtc > startAt
                                )
                            )
                            .OrderBy(_ => Guid.NewGuid())
                            .FirstOrDefault();

                        if (availableVehicle == null)
                        {
                            break;
                        }

                        if (endAt.TimeOfDay > closeTime)
                        {
                            break;
                        }

                        var workOrder = WorkOrder.Create(
                            Guid.NewGuid(),
                            availableVehicle.Id,
                            startAt,
                            endAt,
                            Guid.Parse(laborId),
                            spot,
                            selectedTask,
                            _timeProvider
                        );

                        spotWorkOrders.Add(workOrder.Value);
                        occupiedMinutes += duration;

                        currentTime = startDate.Add(openTime).AddMinutes(occupiedMinutes);
                    }
                    if (occupiedMinutes >= minOccupancy)
                    {
                        generatedWorkOrders.AddRange(spotWorkOrders);
                    }
                }

                startDate = startDate.AddDays(1);
            }

            var repairTasksForFirstOrder = _context
                .RepairTasks.OrderBy(_ => Guid.NewGuid())
                .Take(2)
                .ToList();

            var utcNow = _timeProvider.GetUtcNow();

            var startTimeFirstOrder = utcNow.AddMinutes(15);

            var workOrderStartingNow = WorkOrder
                .Create(
                    Guid.NewGuid(),
                    _context.Vehicles.OrderBy(_ => Guid.NewGuid()).First().Id,
                    startTimeFirstOrder,
                    startTimeFirstOrder.AddMinutes(
                        repairTasksForFirstOrder.Sum(rt => (int)rt.EstimatedDurationInMins)
                    ),
                    Guid.Parse(labor01.Id),
                    Spot.A,
                    repairTasksForFirstOrder,
                    _timeProvider
                )
                .Value;

            workOrderStartingNow.UpdateState(WorkOrderState.InProgress);

            var repairTasksEndingNow = _context.RepairTasks.First(rt =>
                rt.EstimatedDurationInMins == RepairDurationInMinutes.Min60
            );
            var startedAgo = utcNow.AddMinutes(-45);
            var roundedStart = utcNow.AddMinutes(30);

            var endTimeSecondOrder = roundedStart.AddMinutes(
                (int)repairTasksEndingNow.EstimatedDurationInMins
            );

            WorkOrder value = WorkOrder
                .Create(
                    Guid.NewGuid(),
                    _context.Vehicles.OrderBy(_ => Guid.NewGuid()).First().Id,
                    roundedStart,
                    endTimeSecondOrder,
                    Guid.Parse(labor02.Id),
                    Spot.B,
                    [repairTasksEndingNow],
                    _timeProvider
                )
                .Value;
            var workOrderEndingNow = value;

            workOrderEndingNow.UpdateState(WorkOrderState.InProgress);

            generatedWorkOrders.AddRange(workOrderStartingNow, workOrderEndingNow);

            _context.WorkOrders.AddRange(generatedWorkOrders);

            await _context.SaveChangesAsync(CancellationToken.None);
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Configurations\CustomerConfiguration.cs =======

using MechanicShop.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id).IsClustered(false);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);

        builder.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(20);

        builder.Property(c => c.Email).HasMaxLength(150);

        builder.Property(c => c.Address).IsRequired().HasMaxLength(200);

        builder.HasMany(c => c.Vehicles).WithOne().HasForeignKey(v => v.CustomerId);

        builder.Navigation(c => c.Vehicles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Configurations\EmployeeConfiguration.cs =======

using MechanicShop.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id).IsClustered(false);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(50);

        builder.Property(e => e.LastName).IsRequired().HasMaxLength(50);

        builder.Property(e => e.Role).HasConversion<string>().IsRequired();

        builder.Ignore(e => e.FullName);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Configurations\InvoiceConfiguration.cs =======

using MechanicShop.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(i => i.Id).IsClustered(false);
        builder.Property(rt => rt.Id).ValueGeneratedNever();

        builder.Property(i => i.IssuedAtUtc).IsRequired();

        builder.Property(i => i.DiscountAmount).HasPrecision(18, 2).IsRequired();

        builder.Property(i => i.TaxRate).HasPrecision(18, 2).IsRequired();

        builder.Property(i => i.ActualLaborCost).HasPrecision(18, 2).IsRequired();

        builder.Property(i => i.ActualPartsCost).HasPrecision(18, 2).IsRequired();

        builder.Property(i => i.PaidAtUtc);

        builder.Property(i => i.Status).HasConversion<string>().IsRequired();

        builder.Navigation(i => i.LineItems).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(i => i.Total);

        builder.Ignore(i => i.Subtotal);
        builder.Ignore(i => i.TaxAmount);

        builder.OwnsMany(
            i => i.LineItems,
            items =>
            {
                items.ToTable("InvoiceLineItems");

                items.WithOwner().HasForeignKey(i => i.InvoiceId);

                items.HasKey(i => new { i.InvoiceId, i.LineNumber });

                items.Property(i => i.LineNumber).ValueGeneratedNever();

                items.Property(i => i.Description).HasMaxLength(200).IsRequired();

                items.Property(i => i.Quantity).IsRequired();

                items.Property(i => i.UnitPrice).HasPrecision(18, 2).IsRequired();
            }
        );
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Configurations\PartConfiguration.cs =======

using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class PartConfiguration : IEntityTypeConfiguration<Part>
{
    public void Configure(EntityTypeBuilder<Part> builder)
    {
        builder.HasKey(p => p.Id).IsClustered(false);
        builder.Property(rt => rt.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);

        builder.Property(p => p.Cost).IsRequired().HasColumnType("decimal(18,2)");

        builder.Property(p => p.Quantity).IsRequired();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Configurations\RefreshTokenConfiguration.cs =======

using MechanicShop.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id).IsClustered(false);
        builder.Property(rt => rt.Id).ValueGeneratedNever();

        builder.Property(rt => rt.Token).HasMaxLength(200);

        builder.HasIndex(rt => rt.Token).IsUnique();

        builder.Property(rt => rt.UserId).IsRequired();

        builder.Property(rt => rt.ExpiresOnUtc).IsRequired();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Configurations\RepairTaskConfiguration.cs =======

using MechanicShop.Domain.RepairTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class RepairTaskConfiguration : IEntityTypeConfiguration<RepairTask>
{
    public void Configure(EntityTypeBuilder<RepairTask> builder)
    {
        builder.HasKey(rt => rt.Id).IsClustered(false);
        builder.Property(rt => rt.Id).ValueGeneratedNever();

        builder.Property(rt => rt.Name).IsRequired().HasMaxLength(100);

        builder.Property(w => w.EstimatedDurationInMins).HasConversion<string>().IsRequired();

        builder.Property(rt => rt.LaborCost).IsRequired().HasColumnType("decimal(18,2)");

        builder
            .HasMany(c => c.Parts)
            .WithOne()
            .HasForeignKey("RepairTaskId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Parts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(rt => rt.TotalCost);
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Configurations\VehicleConfiguration.cs =======

using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.HasKey(v => v.Id).IsClustered(false);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Make).IsRequired().HasMaxLength(100);

        builder.Property(v => v.Model).IsRequired().HasMaxLength(100);

        builder.HasOne(v => v.Customer).WithMany(c => c.Vehicles).HasForeignKey(v => v.CustomerId);

        builder.Property(v => v.Year).IsRequired();

        builder.Ignore(v => v.VehicleInfo);

        builder.Property(v => v.LicensePlate).IsRequired();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Configurations\WorkOrderConfiguration.cs =======

using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasKey(w => w.Id).IsClustered(false);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.LaborId).IsRequired();

        builder.HasOne(w => w.Labor).WithMany().HasForeignKey(w => w.LaborId).IsRequired();

        builder
            .HasOne(i => i.Invoice)
            .WithOne(w => w.WorkOrder)
            .HasForeignKey<Invoice>(i => i.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(w => w.State).HasConversion<string>().IsRequired();

        builder.Property(w => w.StartAtUtc).IsRequired();

        builder.Property(w => w.EndAtUtc).IsRequired();

        builder.Ignore(w => w.IsEditable);
        builder.Ignore(w => w.Total);
        builder.Ignore(w => w.TotalLaborCost);
        builder.Ignore(w => w.TotalPartsCost);

        builder
            .HasMany(w => w.RepairTasks)
            .WithMany()
            .UsingEntity(j => j.ToTable("WorkOrderRepairTasks"));

        builder.HasOne(w => w.Vehicle).WithMany().HasForeignKey(w => w.VehicleId);

        builder.HasIndex(w => w.LaborId);
        builder.HasIndex(w => w.VehicleId);
        builder.HasIndex(w => w.State);
        builder.HasIndex(a => new { a.StartAtUtc, a.EndAtUtc });

        builder.Property(w => w.Spot).HasConversion<string>().IsRequired();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Interceptors\AuditableEntityInterceptor.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MechanicShop.Infrastructure.Data.Interceptors;

public sealed class AuditableEntityInterceptor(IUser user, TimeProvider time)
    : SaveChangesInterceptor
{
    private readonly IUser _user = user;
    private readonly TimeProvider _time = time;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>().ToList())
        {
            if ((entry.State is EntityState.Added or EntityState.Modified) || entry.HasChangedOwnedEntities())
            {
                if (entry.State is EntityState.Added)
                {
                    entry.Entity.CreateAtUtc = _time.GetUtcNow();
                    entry.Entity.CreateBy = _user.Id;
                }

                entry.Entity.LastModifiedAtUtc = _time.GetUtcNow();
                entry.Entity.LastModifiedBy = _user.Id;
            }

            foreach (var ownedEntry in entry.References)
            {
                if 
                (
                    ownedEntry.TargetEntry != null &&
                    ownedEntry.TargetEntry!.Entity is AuditableEntity ownedEntity &&
                    ownedEntry.TargetEntry.State is EntityState.Added or EntityState.Modified
                )
                {
                    if (ownedEntry.TargetEntry.State == EntityState.Added)
                    {
                        ownedEntity.CreateBy = _user.Id;
                        ownedEntity.CreateAtUtc = _time.GetUtcNow();
                    }
                    ownedEntity.LastModifiedBy = _user.Id;
                    ownedEntity.LastModifiedAtUtc = _time.GetUtcNow();
                }
            }
        }
    }
}

public static class Extension
{
    public static bool HasChangedOwnedEntities(this EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry != null
            && r.TargetEntry.Metadata.IsOwned() == true
            && (
                r.TargetEntry.State == EntityState.Added
                || r.TargetEntry.State == EntityState.Modified
            )
        );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Migrations\20260505111306_InitialCreate.cs =======

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MechanicShop.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(
                        type: "nvarchar(256)",
                        maxLength: 256,
                        nullable: true
                    ),
                    NormalizedName = table.Column<string>(
                        type: "nvarchar(256)",
                        maxLength: 256,
                        nullable: true
                    ),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(
                        type: "nvarchar(256)",
                        maxLength: 256,
                        nullable: true
                    ),
                    NormalizedUserName = table.Column<string>(
                        type: "nvarchar(256)",
                        maxLength: 256,
                        nullable: true
                    ),
                    Email = table.Column<string>(
                        type: "nvarchar(256)",
                        maxLength: 256,
                        nullable: true
                    ),
                    NormalizedEmail = table.Column<string>(
                        type: "nvarchar(256)",
                        maxLength: 256,
                        nullable: true
                    ),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: true
                    ),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(
                        type: "nvarchar(150)",
                        maxLength: 150,
                        nullable: false
                    ),
                    Email = table.Column<string>(
                        type: "nvarchar(150)",
                        maxLength: 150,
                        nullable: false
                    ),
                    PhoneNumber = table.Column<string>(
                        type: "nvarchar(20)",
                        maxLength: 20,
                        nullable: false
                    ),
                    Address = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    CreateAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table
                        .PrimaryKey("PK_Customers", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                }
            );

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    LastName = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table
                        .PrimaryKey("PK_Employees", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                }
            );

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresOnUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    CreateAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table
                        .PrimaryKey("PK_RefreshTokens", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                }
            );

            migrationBuilder.CreateTable(
                name: "RepairTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    EstimatedDurationInMins = table.Column<string>(
                        type: "nvarchar(max)",
                        nullable: false
                    ),
                    LaborCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreateAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table
                        .PrimaryKey("PK_RepairTasks", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                }
            );

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(
                        type: "nvarchar(max)",
                        nullable: true
                    ),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_AspNetUserLogins",
                        x => new { x.LoginProvider, x.ProviderKey }
                    );
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_AspNetUserTokens",
                        x => new
                        {
                            x.UserId,
                            x.LoginProvider,
                            x.Name,
                        }
                    );
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Make = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Model = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LicensePlate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table
                        .PrimaryKey("PK_Vehicles", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_Vehicles_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    RepairTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreateAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table
                        .PrimaryKey("PK_Parts", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_Parts_RepairTasks_RepairTaskId",
                        column: x => x.RepairTaskId,
                        principalTable: "RepairTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LaborId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Spot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    EndAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    State = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreateAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table
                        .PrimaryKey("PK_WorkOrders", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Employees_LaborId",
                        column: x => x.LaborId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_WorkOrders_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    PaidAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: true
                    ),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxRate = table.Column<decimal>(
                        type: "decimal(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false
                    ),
                    DiscountAmount = table.Column<decimal>(
                        type: "decimal(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false
                    ),
                    ActualLaborCost = table.Column<decimal>(
                        type: "decimal(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false
                    ),
                    ActualPartsCost = table.Column<decimal>(
                        type: "decimal(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false
                    ),
                    CreateAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table
                        .PrimaryKey("PK_Invoices", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_Invoices_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "WorkOrderRepairTasks",
                columns: table => new
                {
                    RepairTasksId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_WorkOrderRepairTasks",
                        x => new { x.RepairTasksId, x.WorkOrderId }
                    );
                    table.ForeignKey(
                        name: "FK_WorkOrderRepairTasks_RepairTasks_RepairTasksId",
                        column: x => x.RepairTasksId,
                        principalTable: "RepairTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_WorkOrderRepairTasks_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "InvoiceLineItems",
                columns: table => new
                {
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(
                        type: "decimal(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineItems", x => new { x.InvoiceId, x.LineNumber });
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId"
            );

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId"
            );

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail"
            );

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_WorkOrderId",
                table: "Invoices",
                column: "WorkOrderId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Parts_RepairTaskId",
                table: "Parts",
                column: "RepairTaskId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true,
                filter: "[Token] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CustomerId",
                table: "Vehicles",
                column: "CustomerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRepairTasks_WorkOrderId",
                table: "WorkOrderRepairTasks",
                column: "WorkOrderId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_LaborId",
                table: "WorkOrders",
                column: "LaborId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_StartAtUtc_EndAtUtc",
                table: "WorkOrders",
                columns: new[] { "StartAtUtc", "EndAtUtc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_State",
                table: "WorkOrders",
                column: "State"
            );

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_VehicleId",
                table: "WorkOrders",
                column: "VehicleId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AspNetRoleClaims");

            migrationBuilder.DropTable(name: "AspNetUserClaims");

            migrationBuilder.DropTable(name: "AspNetUserLogins");

            migrationBuilder.DropTable(name: "AspNetUserRoles");

            migrationBuilder.DropTable(name: "AspNetUserTokens");

            migrationBuilder.DropTable(name: "InvoiceLineItems");

            migrationBuilder.DropTable(name: "Parts");

            migrationBuilder.DropTable(name: "RefreshTokens");

            migrationBuilder.DropTable(name: "WorkOrderRepairTasks");

            migrationBuilder.DropTable(name: "AspNetRoles");

            migrationBuilder.DropTable(name: "AspNetUsers");

            migrationBuilder.DropTable(name: "Invoices");

            migrationBuilder.DropTable(name: "RepairTasks");

            migrationBuilder.DropTable(name: "WorkOrders");

            migrationBuilder.DropTable(name: "Employees");

            migrationBuilder.DropTable(name: "Vehicles");

            migrationBuilder.DropTable(name: "Customers");
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Migrations\20260505111306_InitialCreate.Designer.cs =======

// <auto-generated />
using System;
using MechanicShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace MechanicShop.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260505111306_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Employee", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.ToTable("Employees");
                });

            modelBuilder.Entity("MechanicShop.Domain.Customers.Customer", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<string>("PhoneNumber")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.ToTable("Customers");
                });

            modelBuilder.Entity("MechanicShop.Domain.Customers.Vehicle", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("CustomerId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("LicensePlate")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Make")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("Model")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<int>("Year")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("CustomerId");

                    b.ToTable("Vehicles");
                });

            modelBuilder.Entity("MechanicShop.Domain.Identity.RefreshToken", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("ExpiresOnUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Token")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("Token")
                        .IsUnique()
                        .HasFilter("[Token] IS NOT NULL");

                    b.ToTable("RefreshTokens", (string)null);
                });

            modelBuilder.Entity("MechanicShop.Domain.Invoices.Invoice", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("ActualLaborCost")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("ActualPartsCost")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("DiscountAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("IssuedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset?>("PaidAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("TaxRate")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<Guid>("WorkOrderId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("WorkOrderId")
                        .IsUnique();

                    b.ToTable("Invoices", (string)null);
                });

            modelBuilder.Entity("MechanicShop.Domain.RepairTasks.Part", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Cost")
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<int>("Quantity")
                        .HasColumnType("int");

                    b.Property<Guid?>("RepairTaskId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("RepairTaskId");

                    b.ToTable("Parts");
                });

            modelBuilder.Entity("MechanicShop.Domain.RepairTasks.RepairTask", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("EstimatedDurationInMins")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("LaborCost")
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.ToTable("RepairTasks");
                });

            modelBuilder.Entity("MechanicShop.Domain.WorkOrders.WorkOrder", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("EndAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("LaborId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Spot")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("StartAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("State")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<Guid>("VehicleId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("LaborId");

                    b.HasIndex("State");

                    b.HasIndex("VehicleId");

                    b.HasIndex("StartAtUtc", "EndAtUtc");

                    b.ToTable("WorkOrders");
                });

            modelBuilder.Entity("MechanicShop.Infrastructure.Identity.AppUser", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("int");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("bit");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("bit");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("bit");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("bit");

                    b.Property<string>("UserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasDatabaseName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasDatabaseName("UserNameIndex")
                        .HasFilter("[NormalizedUserName] IS NOT NULL");

                    b.ToTable("AspNetUsers", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasDatabaseName("RoleNameIndex")
                        .HasFilter("[NormalizedName] IS NOT NULL");

                    b.ToTable("AspNetRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RoleId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderKey")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderDisplayName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("RoleId")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Value")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens", (string)null);
                });

            modelBuilder.Entity("RepairTaskWorkOrder", b =>
                {
                    b.Property<Guid>("RepairTasksId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("WorkOrderId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("RepairTasksId", "WorkOrderId");

                    b.HasIndex("WorkOrderId");

                    b.ToTable("WorkOrderRepairTasks", (string)null);
                });

            modelBuilder.Entity("MechanicShop.Domain.Customers.Vehicle", b =>
                {
                    b.HasOne("MechanicShop.Domain.Customers.Customer", "Customer")
                        .WithMany("Vehicles")
                        .HasForeignKey("CustomerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Customer");
                });

            modelBuilder.Entity("MechanicShop.Domain.Invoices.Invoice", b =>
                {
                    b.HasOne("MechanicShop.Domain.WorkOrders.WorkOrder", "WorkOrder")
                        .WithOne("Invoice")
                        .HasForeignKey("MechanicShop.Domain.Invoices.Invoice", "WorkOrderId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.OwnsMany("MechanicShop.Domain.Invoices.InvoiceLineItem", "LineItems", b1 =>
                        {
                            b1.Property<Guid>("InvoiceId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<int>("LineNumber")
                                .HasColumnType("int");

                            b1.Property<string>("Description")
                                .IsRequired()
                                .HasMaxLength(200)
                                .HasColumnType("nvarchar(200)");

                            b1.Property<int>("Quantity")
                                .HasColumnType("int");

                            b1.Property<decimal>("UnitPrice")
                                .HasPrecision(18, 2)
                                .HasColumnType("decimal(18,2)");

                            b1.HasKey("InvoiceId", "LineNumber");

                            b1.ToTable("InvoiceLineItems", (string)null);

                            b1.WithOwner()
                                .HasForeignKey("InvoiceId");
                        });

                    b.Navigation("LineItems");

                    b.Navigation("WorkOrder");
                });

            modelBuilder.Entity("MechanicShop.Domain.RepairTasks.Part", b =>
                {
                    b.HasOne("MechanicShop.Domain.RepairTasks.RepairTask", null)
                        .WithMany("Parts")
                        .HasForeignKey("RepairTaskId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("MechanicShop.Domain.WorkOrders.WorkOrder", b =>
                {
                    b.HasOne("Employee", "Labor")
                        .WithMany()
                        .HasForeignKey("LaborId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MechanicShop.Domain.Customers.Vehicle", "Vehicle")
                        .WithMany()
                        .HasForeignKey("VehicleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Labor");

                    b.Navigation("Vehicle");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("MechanicShop.Infrastructure.Identity.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("MechanicShop.Infrastructure.Identity.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MechanicShop.Infrastructure.Identity.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("MechanicShop.Infrastructure.Identity.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("RepairTaskWorkOrder", b =>
                {
                    b.HasOne("MechanicShop.Domain.RepairTasks.RepairTask", null)
                        .WithMany()
                        .HasForeignKey("RepairTasksId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MechanicShop.Domain.WorkOrders.WorkOrder", null)
                        .WithMany()
                        .HasForeignKey("WorkOrderId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MechanicShop.Domain.Customers.Customer", b =>
                {
                    b.Navigation("Vehicles");
                });

            modelBuilder.Entity("MechanicShop.Domain.RepairTasks.RepairTask", b =>
                {
                    b.Navigation("Parts");
                });

            modelBuilder.Entity("MechanicShop.Domain.WorkOrders.WorkOrder", b =>
                {
                    b.Navigation("Invoice");
                });
#pragma warning restore 612, 618
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Data\Migrations\AppDbContextModelSnapshot.cs =======

// <auto-generated />
using System;
using MechanicShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace MechanicShop.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Employee", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.ToTable("Employees");
                });

            modelBuilder.Entity("MechanicShop.Domain.Customers.Customer", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<string>("PhoneNumber")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.ToTable("Customers");
                });

            modelBuilder.Entity("MechanicShop.Domain.Customers.Vehicle", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("CustomerId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("LicensePlate")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Make")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("Model")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<int>("Year")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("CustomerId");

                    b.ToTable("Vehicles");
                });

            modelBuilder.Entity("MechanicShop.Domain.Identity.RefreshToken", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("ExpiresOnUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Token")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("Token")
                        .IsUnique()
                        .HasFilter("[Token] IS NOT NULL");

                    b.ToTable("RefreshTokens", (string)null);
                });

            modelBuilder.Entity("MechanicShop.Domain.Invoices.Invoice", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("ActualLaborCost")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("ActualPartsCost")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("DiscountAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("IssuedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset?>("PaidAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("TaxRate")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<Guid>("WorkOrderId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("WorkOrderId")
                        .IsUnique();

                    b.ToTable("Invoices", (string)null);
                });

            modelBuilder.Entity("MechanicShop.Domain.RepairTasks.Part", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Cost")
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<int>("Quantity")
                        .HasColumnType("int");

                    b.Property<Guid?>("RepairTaskId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("RepairTaskId");

                    b.ToTable("Parts");
                });

            modelBuilder.Entity("MechanicShop.Domain.RepairTasks.RepairTask", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("EstimatedDurationInMins")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("LaborCost")
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.ToTable("RepairTasks");
                });

            modelBuilder.Entity("MechanicShop.Domain.WorkOrders.WorkOrder", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreateAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreateBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("EndAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("LaborId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("LastModifiedAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Spot")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("StartAtUtc")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("State")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<Guid>("VehicleId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.HasIndex("LaborId");

                    b.HasIndex("State");

                    b.HasIndex("VehicleId");

                    b.HasIndex("StartAtUtc", "EndAtUtc");

                    b.ToTable("WorkOrders");
                });

            modelBuilder.Entity("MechanicShop.Infrastructure.Identity.AppUser", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("int");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("bit");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("bit");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("bit");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("bit");

                    b.Property<string>("UserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasDatabaseName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasDatabaseName("UserNameIndex")
                        .HasFilter("[NormalizedUserName] IS NOT NULL");

                    b.ToTable("AspNetUsers", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasDatabaseName("RoleNameIndex")
                        .HasFilter("[NormalizedName] IS NOT NULL");

                    b.ToTable("AspNetRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RoleId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderKey")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderDisplayName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("RoleId")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Value")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens", (string)null);
                });

            modelBuilder.Entity("RepairTaskWorkOrder", b =>
                {
                    b.Property<Guid>("RepairTasksId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("WorkOrderId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("RepairTasksId", "WorkOrderId");

                    b.HasIndex("WorkOrderId");

                    b.ToTable("WorkOrderRepairTasks", (string)null);
                });

            modelBuilder.Entity("MechanicShop.Domain.Customers.Vehicle", b =>
                {
                    b.HasOne("MechanicShop.Domain.Customers.Customer", "Customer")
                        .WithMany("Vehicles")
                        .HasForeignKey("CustomerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Customer");
                });

            modelBuilder.Entity("MechanicShop.Domain.Invoices.Invoice", b =>
                {
                    b.HasOne("MechanicShop.Domain.WorkOrders.WorkOrder", "WorkOrder")
                        .WithOne("Invoice")
                        .HasForeignKey("MechanicShop.Domain.Invoices.Invoice", "WorkOrderId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.OwnsMany("MechanicShop.Domain.Invoices.InvoiceLineItem", "LineItems", b1 =>
                        {
                            b1.Property<Guid>("InvoiceId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<int>("LineNumber")
                                .HasColumnType("int");

                            b1.Property<string>("Description")
                                .IsRequired()
                                .HasMaxLength(200)
                                .HasColumnType("nvarchar(200)");

                            b1.Property<int>("Quantity")
                                .HasColumnType("int");

                            b1.Property<decimal>("UnitPrice")
                                .HasPrecision(18, 2)
                                .HasColumnType("decimal(18,2)");

                            b1.HasKey("InvoiceId", "LineNumber");

                            b1.ToTable("InvoiceLineItems", (string)null);

                            b1.WithOwner()
                                .HasForeignKey("InvoiceId");
                        });

                    b.Navigation("LineItems");

                    b.Navigation("WorkOrder");
                });

            modelBuilder.Entity("MechanicShop.Domain.RepairTasks.Part", b =>
                {
                    b.HasOne("MechanicShop.Domain.RepairTasks.RepairTask", null)
                        .WithMany("Parts")
                        .HasForeignKey("RepairTaskId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("MechanicShop.Domain.WorkOrders.WorkOrder", b =>
                {
                    b.HasOne("Employee", "Labor")
                        .WithMany()
                        .HasForeignKey("LaborId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MechanicShop.Domain.Customers.Vehicle", "Vehicle")
                        .WithMany()
                        .HasForeignKey("VehicleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Labor");

                    b.Navigation("Vehicle");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("MechanicShop.Infrastructure.Identity.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("MechanicShop.Infrastructure.Identity.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MechanicShop.Infrastructure.Identity.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("MechanicShop.Infrastructure.Identity.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("RepairTaskWorkOrder", b =>
                {
                    b.HasOne("MechanicShop.Domain.RepairTasks.RepairTask", null)
                        .WithMany()
                        .HasForeignKey("RepairTasksId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MechanicShop.Domain.WorkOrders.WorkOrder", null)
                        .WithMany()
                        .HasForeignKey("WorkOrderId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MechanicShop.Domain.Customers.Customer", b =>
                {
                    b.Navigation("Vehicles");
                });

            modelBuilder.Entity("MechanicShop.Domain.RepairTasks.RepairTask", b =>
                {
                    b.Navigation("Parts");
                });

            modelBuilder.Entity("MechanicShop.Domain.WorkOrders.WorkOrder", b =>
                {
                    b.Navigation("Invoice");
                });
#pragma warning restore 612, 618
        }
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Identity\AppUser.cs =======

using Microsoft.AspNetCore.Identity;

namespace MechanicShop.Infrastructure.Identity;

public sealed class AppUser : IdentityUser;

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Identity\IdentityService.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Domain.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace MechanicShop.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<AppUser> userManager,
    IUserClaimsPrincipalFactory<AppUser> userClaimsPrincipal,
    IAuthorizationService authorizationService
) : IIdentityService
{
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly IUserClaimsPrincipalFactory<AppUser> _userClaimsPrincipal =
        userClaimsPrincipal;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<bool> IsInRoleAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user != null && await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<bool> AuthorizeAsync(string userId, string? policyName)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return false;
        }

        var principle = await _userClaimsPrincipal.CreateAsync(user);
        var result = await _authorizationService.AuthorizeAsync(principle, policyName!);

        return result.Succeeded;
    }

    public async Task<Result<UserInformationDto>> AuthenticateAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            return Error.Conflict("Invalid_Login_Attempt", "Email / Password are incorrect");
        }
        if (!user.EmailConfirmed)
        {
            return Error.Conflict(
                "Email_Not_Confirmed",
                $"email '{UtilityService.MaskEmail(email)}' not confirmed"
            );
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            return Error.Conflict("Invalid_Login_Attempt", "Email / Password are incorrect");
        }

        return new UserInformationDto(
            user.Id,
            email,
            await _userManager.GetRolesAsync(user),
            await _userManager.GetClaimsAsync(user)
        );
    }

    public async Task<Result<UserInformationDto>> GetUserByIdAsync(string userId)
    {
        var user =
            await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException();
        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);

        return new UserInformationDto(user.Id, user.Email!, roles, claims);
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user?.UserName;
    }

    public async Task<Result<Guid>> CreateUserAsync(string email, string password, string role)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            return Error.Conflict("User_Exists", $"User with email: {email} already exists");
        }

        var user = new AppUser { UserName = email, Email = email };

        var createdResult = await _userManager.CreateAsync(user, password);

        if (!createdResult.Succeeded)
        {
            var errors = createdResult
                .Errors.Select(e => Error.Failure(e.Code, e.Description))
                .ToList();

            return errors;
        }

        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _userManager.ConfirmEmailAsync(user, confirmationToken);

        if (!string.IsNullOrEmpty(role))
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        return Guid.Parse(user.Id);
    }

    public async Task<Result<Deleted>> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Result.Deleted;
        }

        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => Error.Failure(e.Code, e.Description)).ToList();

            return errors;
        }

        return Result.Deleted;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Identity\LaborAssignedRequirement.cs =======

using System.Security.Claims;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Identity;

public sealed class LaborAssignedRequirement : IAuthorizationRequirement;

public sealed class LaborAssignedHandler(
    IAppDbContext context,
    IHttpContextAccessor httpContextAccessor
) : AuthorizationHandler<LaborAssignedRequirement>
{
    private readonly IAppDbContext _context = context;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LaborAssignedRequirement requirement
    )
    {
        if (context.User.IsInRole(nameof(Role.Manager)))
        {
            context.Succeed(requirement);
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || string.IsNullOrEmpty(userIdClaim.Value))
        {
            context.Fail();
            return;
        }

        var userId = userIdClaim.Value;

        var httpContext = _httpContextAccessor.HttpContext;
        if (
            httpContext is null
            || !httpContext.Request.RouteValues.TryGetValue("workOrderId", out var routeValue)
            || routeValue is null
        )
        {
            context.Fail();
            return;
        }

        var workOrderString = routeValue.ToString();

        if (!Guid.TryParse(workOrderString, out var workOrderId))
        {
            context.Fail();
            return;
        }

        var isAssigned = await _context.WorkOrders.AnyAsync(wo =>
            wo.Id == workOrderId && wo.LaborId == Guid.Parse(userId)
        );

        if (isAssigned)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Identity\TokenProvider.cs =======

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Domain.Common.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MechanicShop.Infrastructure.Identity;

public sealed class TokenProvider(IConfiguration configuration, TimeProvider timeProvider)
    : ITokenProvider
{
    private readonly IConfiguration _configuration = configuration;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Result<TokenDto>> GenerateJwtTokenAsync(
        UserInformationDto user,
        CancellationToken ct = default
    )
    {
        var tokenResult = await CreateAsync(user, ct);

        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }
        return tokenResult.Value;
    }

    private async Task<Result<TokenDto>> CreateAsync(UserInformationDto user, CancellationToken ct)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");

        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var key = jwtSettings["Secret"];
        var expiry = _timeProvider
            .GetUtcNow()
            .AddMinutes(int.Parse(jwtSettings["TokenExpirationInMinutes"]!));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(JwtRegisteredClaimNames.Email, user.Email),
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
        }

        var descriptor = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer,
            Audience = audience,
            Expires = expiry.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)),
                SecurityAlgorithms.HmacSha256Signature
            ),
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(descriptor);

        var accessToken = tokenHandler.WriteToken(securityToken);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresOnUtc = expiry.DateTime,
        };
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var tokenValidationParameter = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["JwtSettings:Secret"]!)
            ),
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        var principal = tokenHandler.ValidateToken(
            token,
            tokenValidationParameter,
            out SecurityToken securityToken
        );
        if (
            securityToken is not JwtSecurityToken jwtSecurityToken
            || !jwtSecurityToken.Header.Alg.Equals(
                SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase
            )
        )
        {
            throw new SecurityTokenException("Invalid token.");
        }
        return principal;
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\RealTime\SignalRWorkOrderNotifier.cs =======

using MechanicShop.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace MechanicShop.Infrastructure.RealTime;

public sealed class SignalRWorkOrderNotifier(IHubContext<WorkOrderHub> hubContext)
    : IWorkOrderNotifier
{
    private readonly IHubContext<WorkOrderHub> _hubContext = hubContext;

    public Task NotifyWorkOrdersChangedAsync(
        Guid workOrderId,
        string eventType,
        CancellationToken ct = default
    ) =>
        _hubContext.Clients.All.SendAsync(
            "WorkOrdersChanged",
            new { WorkOrderId = workOrderId, EventType = eventType },
            cancellationToken: ct
        );
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\RealTime\WorkOrderHub.cs =======

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MechanicShop.Infrastructure.RealTime;

[Authorize]
public sealed class WorkOrderHub : Hub
{
    public const string HubUrl = "/hubs/workOrders";
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Services\InvoicePdfGenerator.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Invoices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MechanicShop.Infrastructure.Services;

public sealed class InvoicePdfGenerator : IInvoicePdfGenerator
{
    public byte[] Generate(Invoice invoice)
    {
        return Document
            .Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().Element(BuildHeader(invoice));
                    page.Content().Element(BuildInvoiceContent(invoice));
                    page.Footer().Element(BuildFooter());
                });
            })
            .GeneratePdf();
    }

    private Action<IContainer> BuildHeader(Invoice invoice) =>
        header =>
        {
            header.Column(col =>
            {
                // Logo and Company Name Row
                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem(1)
                            .Element(container =>
                            {
                                container.Row(logoRow =>
                                {
                                    // Car repair icon placeholder
                                    logoRow
                                        .ConstantItem(50)
                                        .Height(50)
                                        .Background(Colors.Red.Medium)
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .Text("🔧")
                                        .FontSize(24)
                                        .FontColor(Colors.White);

                                    // Company name with colorful letters
                                    logoRow
                                        .RelativeItem()
                                        .PaddingLeft(15)
                                        .AlignMiddle()
                                        .Text(text =>
                                        {
                                            text.Span("M")
                                                .FontColor(Colors.Cyan.Lighten3)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("e")
                                                .FontColor(Colors.Orange.Medium)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("c")
                                                .FontColor(Colors.Yellow.Medium)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("h")
                                                .FontColor(Colors.Green.Medium)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("a")
                                                .FontColor(Colors.Blue.Lighten2)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("n")
                                                .FontColor(Colors.Red.Lighten2)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("i")
                                                .FontColor(Colors.Purple.Medium)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("c")
                                                .FontColor(Colors.Brown.Medium)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span(" ").FontSize(24);
                                            text.Span("S")
                                                .FontColor(Colors.Red.Darken1)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("h")
                                                .FontColor(Colors.Red.Medium)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("o")
                                                .FontColor(Colors.Green.Lighten1)
                                                .FontSize(24)
                                                .Bold();
                                            text.Span("P")
                                                .FontColor(Colors.Pink.Lighten2)
                                                .FontSize(24)
                                                .Bold();
                                        });
                                });
                            });

                        // Invoice details
                        row.RelativeItem(1)
                            .AlignRight()
                            .Column(detailsCol =>
                            {
                                detailsCol
                                    .Item()
                                    .Text($"INVOICE #{invoice.Id.ToString().Substring(0, 8)}")
                                    .FontSize(28)
                                    .Bold()
                                    .FontColor(Colors.Grey.Darken3);

                                detailsCol
                                    .Item()
                                    .PaddingTop(5)
                                    .Text($"Date: {invoice.IssuedAtUtc:MMMM dd, yyyy}")
                                    .FontSize(12)
                                    .FontColor(Colors.Grey.Medium);

                                detailsCol
                                    .Item()
                                    .Text($"Status: {invoice.Status}")
                                    .FontSize(12)
                                    .FontColor(GetStatusColor(invoice.Status.ToString()));
                            });
                    });

                // Separator line
                col.Item().PaddingVertical(20).LineHorizontal(2).LineColor(Colors.Grey.Darken2);
            });
        };

    private Action<IContainer> BuildInvoiceContent(Invoice invoice) =>
        content =>
        {
            content.Column(col =>
            {
                // Professional table with enhanced styling
                col.Item()
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4); // Description
                            columns.RelativeColumn(1); // Qty
                            columns.RelativeColumn(2); // Unit Price
                            columns.RelativeColumn(2); // Line Total
                        });

                        // Enhanced header
                        table.Header(header =>
                        {
                            header
                                .Cell()
                                .Background(Colors.Grey.Darken2)
                                .Padding(12)
                                .Text("DESCRIPTION")
                                .Bold()
                                .FontColor(Colors.White)
                                .FontSize(11);

                            header
                                .Cell()
                                .Background(Colors.Grey.Darken2)
                                .Padding(12)
                                .AlignCenter()
                                .Text("QTY")
                                .Bold()
                                .FontColor(Colors.White)
                                .FontSize(11);

                            header
                                .Cell()
                                .Background(Colors.Grey.Darken2)
                                .Padding(12)
                                .AlignCenter()
                                .Text("UNIT PRICE")
                                .Bold()
                                .FontColor(Colors.White)
                                .FontSize(11);

                            header
                                .Cell()
                                .Background(Colors.Grey.Darken2)
                                .Padding(12)
                                .AlignRight()
                                .Text("LINE TOTAL")
                                .Bold()
                                .FontColor(Colors.White)
                                .FontSize(11);
                        });

                        // Enhanced rows with alternating colors
                        var isEvenRow = false;
                        foreach (var item in invoice.LineItems)
                        {
                            var backgroundColor = isEvenRow ? Colors.Grey.Lighten4 : Colors.White;

                            table
                                .Cell()
                                .Background(backgroundColor)
                                .Padding(12)
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Text(item.Description)
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken3);

                            table
                                .Cell()
                                .Background(backgroundColor)
                                .Padding(12)
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .AlignCenter()
                                .Text(item.Quantity.ToString())
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken3);

                            table
                                .Cell()
                                .Background(backgroundColor)
                                .Padding(12)
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .AlignCenter()
                                .Text($"{item.UnitPrice:C}")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken3);

                            table
                                .Cell()
                                .Background(backgroundColor)
                                .Padding(12)
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .AlignRight()
                                .Text($"{item.LineTotal:C}")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken3)
                                .Bold();

                            isEvenRow = !isEvenRow;
                        }
                    });

                // Enhanced totals section
                col.Item()
                    .PaddingTop(30)
                    .Row(row =>
                    {
                        row.RelativeItem(2); // Empty space

                        row.RelativeItem(1)
                            .Column(totalsCol =>
                            {
                                totalsCol
                                    .Item()
                                    .BorderTop(1)
                                    .BorderColor(Colors.Grey.Medium)
                                    .PaddingTop(10);

                                totalsCol
                                    .Item()
                                    .PaddingVertical(5)
                                    .Row(totalRow =>
                                    {
                                        totalRow
                                            .RelativeItem()
                                            .Text("Subtotal:")
                                            .FontSize(11)
                                            .FontColor(Colors.Grey.Medium);
                                        totalRow
                                            .RelativeItem()
                                            .AlignRight()
                                            .Text($"{invoice.Subtotal:C}")
                                            .FontSize(11)
                                            .FontColor(Colors.Grey.Darken3);
                                    });

                                totalsCol
                                    .Item()
                                    .PaddingVertical(5)
                                    .Row(totalRow =>
                                    {
                                        totalRow
                                            .RelativeItem()
                                            .Text("Tax:")
                                            .FontSize(11)
                                            .FontColor(Colors.Grey.Medium);
                                        totalRow
                                            .RelativeItem()
                                            .AlignRight()
                                            .Text($"{invoice.TaxAmount:C}")
                                            .FontSize(11)
                                            .FontColor(Colors.Grey.Darken3);
                                    });

                                if (invoice.DiscountAmount > 0)
                                {
                                    totalsCol
                                        .Item()
                                        .PaddingVertical(5)
                                        .Row(totalRow =>
                                        {
                                            totalRow
                                                .RelativeItem()
                                                .Text("Discount:")
                                                .FontSize(11)
                                                .FontColor(Colors.Red.Medium);
                                            totalRow
                                                .RelativeItem()
                                                .AlignRight()
                                                .Text($"-{invoice.DiscountAmount:C}")
                                                .FontSize(11)
                                                .FontColor(Colors.Red.Medium);
                                        });
                                }

                                totalsCol
                                    .Item()
                                    .BorderTop(2)
                                    .BorderColor(Colors.Grey.Darken3)
                                    .PaddingTop(10)
                                    .PaddingVertical(5)
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(10)
                                    .Row(totalRow =>
                                    {
                                        totalRow
                                            .RelativeItem()
                                            .Text("TOTAL:")
                                            .FontSize(14)
                                            .Bold()
                                            .FontColor(Colors.Grey.Darken3);
                                        totalRow
                                            .RelativeItem()
                                            .AlignRight()
                                            .Text($"{invoice.Total:C}")
                                            .FontSize(16)
                                            .Bold()
                                            .FontColor(Colors.Green.Medium);
                                    });
                            });
                    });
            });
        };

    private Action<IContainer> BuildFooter() =>
        footer =>
        {
            footer.Row(row =>
            {
                row.RelativeItem()
                    .AlignLeft()
                    .Text("Drive safe. See you next time!")
                    .FontSize(10)
                    .FontColor(Colors.Grey.Medium)
                    .Italic();

                row.RelativeItem()
                    .AlignRight()
                    .Text(text =>
                    {
                        text.Span("Generated on ").FontSize(9).FontColor(Colors.Grey.Medium);
                        text.Span($"{DateTime.UtcNow:MMMM dd, yyyy 'at' HH:mm} UTC")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Medium)
                            .SemiBold();
                    });
            });
        };

    private string GetStatusColor(string status)
    {
        return status.ToLower() switch
        {
            "paid" => Colors.Green.Medium,
            "Scheduled" => Colors.Orange.Medium,
            "overdue" => Colors.Red.Medium,
            "cancelled" => Colors.Grey.Medium,
            _ => Colors.Grey.Medium,
        };
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Services\WorkOrderPolicy.cs =======

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MechanicShop.Infrastructure.Services;

public sealed class WorkOrderPolicy(IOptions<AppSettings> options, IAppDbContext context)
    : IWorkOrderPolicy
{
    private readonly AppSettings _appSettings = options.Value;
    private readonly IAppDbContext _context = context;

    public async Task<Result<Success>> CheckSpotAvailabilityAsync(
        Spot spot,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludeWorkOrderId = null,
        CancellationToken ct = default
    )
    {
        var isOccupied = await _context.WorkOrders.AnyAsync(
            a =>
                a.Spot == spot
                && a.StartAtUtc < endAt
                && a.EndAtUtc > startAt
                && (!excludeWorkOrderId.HasValue || a.Id != excludeWorkOrderId.Value),
            ct
        );

        return isOccupied
            ? Error.Conflict(
                "MechanicShop_Spot_Full",
                "The selected time slot is unavailable for the requested services."
            )
            : Result.Success;
    }

    public Result<Success> ValidateMinimumRequirement(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        if (
            (endAt - startAt)
            < TimeSpan.FromMinutes(_appSettings.MinimumAppointmentDurationInMinutes)
        )
        {
            return Error.Conflict(
                "WorkOrder_TooShort",
                $"WorkOrder duration must be at least {_appSettings.MinimumAppointmentDurationInMinutes} minutes."
            );
        }

        return Result.Success;
    }

    public async Task<bool> IsLaborOccupied(
        Guid laborId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludedWorkOrderId = null
    )
    {
        return await _context.WorkOrders.AnyAsync(a =>
            a.LaborId == laborId
            && a.Id != excludedWorkOrderId
            && a.StartAtUtc < endAt
            && a.EndAtUtc > startAt
        );
    }

    public bool IsOutsideOperatingHours(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(_appSettings.StoreTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
        }
        var localStartAt = TimeZoneInfo.ConvertTime(startAt, timeZone);
        var localEndAt = TimeZoneInfo.ConvertTime(endAt, timeZone);

        if (localStartAt.Date != localEndAt.Date)
        {
            return true;
        }
        var startTimeOnly = TimeOnly.FromTimeSpan(localStartAt.TimeOfDay);
        var endTimeOnly = TimeOnly.FromTimeSpan(localEndAt.TimeOfDay);

        if (startTimeOnly < _appSettings.OpeningTime || endTimeOnly > _appSettings.ClosingTime)
        {
            return true;
        }

        return false;
    }

    public async Task<bool> IsVehicleAlreadyScheduled(
        Guid vehicleId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludedWorkOrderId = null
    )
    {
        return await _context.WorkOrders.AnyAsync(a =>
            a.VehicleId == vehicleId
            && (excludedWorkOrderId == null || a.Id != excludedWorkOrderId)
            && a.StartAtUtc < endAt
            && a.EndAtUtc > startAt
        );
    }
}

======= C:\Users\HP\OneDrive\Desktop\MechanicShop\src\MechanicShop.Infrastructure\Settings\AppSettings.cs =======

namespace MechanicShop.Infrastructure.Settings;

public sealed class AppSettings
{
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }
    public string StoreTimeZoneId { get; set; } = default!;
    public int MaxSpots { get; set; }
    public int MinimumAppointmentDurationInMinutes { get; set; }
    public int LocalCacheExpirationInMins { get; set; }
    public int DistributedCacheExpirationMins { get; set; }
    public int DefaultPageNumber { get; set; }
    public int DefaultPageSize { get; set; }
    public int BookingCancellationThresholdMinutes { get; set; }
    public int OverdueBookingCleanupFrequencyMinutes { get; set; }
    public string CorsPolicyName { get; set; } = default!;
    public string[] AllowedOrigins { get; set; } = default!;
}
