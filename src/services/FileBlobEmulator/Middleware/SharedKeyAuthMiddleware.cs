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

        // If no Authorization header, allow request (for backwards compatibility)
        // You may want to change this to require auth in production
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            await _next(context);
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

        // For other auth schemes, continue (could add Bearer token support here)
        await _next(context);
    }
}