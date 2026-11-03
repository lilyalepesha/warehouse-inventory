using System.Text.Json;
using Warehouse.Api.Domain;

namespace Warehouse.Api.Infrastructure;

/// <summary>Превращает бизнес-исключения в единый JSON-ответ API.</summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException exception)
        {
            await WriteErrorAsync(context, exception.StatusCode, exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Необработанная ошибка при обработке {Path}", context.Request.Path);
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Внутренняя ошибка сервера. Проверьте журнал приложения.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}
