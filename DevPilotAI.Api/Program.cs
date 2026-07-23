using DevPilotAI.Api.Configuration;
using DevPilotAI.Api.Middleware;
using DevPilotAI.Application;
using DevPilotAI.Infrastructure;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.ConfigureSerilog();

try
{
    Log.Information("Starting web host...");

    // Add services to the container
    builder.Services.AddControllers();
    
    // Register Clean Architecture layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Register Swagger & Health Checks
    builder.Services.AddSwaggerDocumentation();
    builder.Services.AddHealthChecks();

    // CORS policy for Dev/Frontend communication
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DefaultPolicy", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerDocumentation();
    }

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    app.UseCors("DefaultPolicy");

    app.UseAuthorization();

    app.MapControllers();

    // Map custom JSON health checks
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                Status = report.Status.ToString(),
                Duration = report.TotalDuration,
                Checks = report.Entries.Select(e => new
                {
                    Component = e.Key,
                    Status = e.Value.Status.ToString(),
                    Description = e.Value.Description,
                    Duration = e.Value.Duration
                })
            };
            await context.Response.WriteAsJsonAsync(response);
        }
    });

    // Seed database on startup
    using (var scope = app.Services.CreateScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();
        try
        {
            await initializer.SeedAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "An error occurred during database seeding. Ensure migrations are applied.");
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
