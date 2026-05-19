using FileBlobEmulator.Services;

namespace FileBlobEmulator.Middleware;

public class SharedKeyAuthMiddleware
{
    private readonly RequestDelegate _next;

    public SharedKeyAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SharedKeyValidator validator)
    {
        // Skip auth for Swagger/OpenAPI endpoints
        if (context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/openapi"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            await RejectAsync(context);
            return;
        }

        var auth = authHeader.ToString();

        // If it's a SharedKey request, validate the signature
        if (auth.StartsWith("SharedKey ", StringComparison.OrdinalIgnoreCase))
        {
            if (validator.Validate(context.Request))
            {
                await _next(context);
                return;
            }

            // Invalid signature or account
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Authentication failed: Invalid signature or account.");
            return;
        }

        await RejectAsync(context);
    }

    private static async Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Authentication failed: SharedKey authorization is required.");
    }
}
