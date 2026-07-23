using System.Net;
using System.Text.Json;
using DevPilotAI.Shared.Common;
using FluentValidation;

namespace DevPilotAI.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = HttpStatusCode.InternalServerError;
        string message = "An unexpected error occurred.";
        List<string> errors = new();

        if (exception is ValidationException validationException)
        {
            statusCode = HttpStatusCode.BadRequest;
            message = "Validation failed.";
            errors.AddRange(validationException.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
        }
        else
        {
            if (_env.IsDevelopment())
            {
                message = exception.Message;
                if (exception.InnerException != null)
                {
                    errors.Add($"Inner Exception: {exception.InnerException.Message}");
                }
                errors.Add(exception.StackTrace ?? string.Empty);
            }
            else
            {
                // In production, keep exception details hidden
                errors.Add("Please contact system administrator.");
            }
        }

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse.Failure(message, errors);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(response, options);
        await context.Response.WriteAsync(json);
    }
}
