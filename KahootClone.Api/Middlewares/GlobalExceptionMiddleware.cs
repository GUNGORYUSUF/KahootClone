using System.Net;
using System.Text.Json;

namespace KahootClone.Api.Middlewares;

// Sınıf tanımının yanına parametreler (Primary Constructor) eklendi ve gereksiz atama blokları silindi.
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API İsteğinde beklenmeyen bir hata oluştu: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new { Error = "Sunucu tarafında bir hata oluştu.", Detail = exception.Message };
        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}