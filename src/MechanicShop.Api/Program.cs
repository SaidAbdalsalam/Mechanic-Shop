using MechanicShop.Api.DependencyInjection;
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

app.UseCoreMiddlewares(builder.Configuration);

app.MapControllers();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapHub<WorkOrderHub>("/hubs/workorders");

await app.Services.InitializeDatabaseAsync();

app.Run();
