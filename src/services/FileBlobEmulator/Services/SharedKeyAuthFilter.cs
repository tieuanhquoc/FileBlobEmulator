using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FileBlobEmulator.Services;

public class SharedKeyAuthFilter : IAsyncActionFilter
{
    private readonly SharedKeyValidator _validator;
    private readonly ILogger<SharedKeyAuthFilter> _logger;

    public SharedKeyAuthFilter(SharedKeyValidator validator, ILogger<SharedKeyAuthFilter> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? "";

        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        if (!_validator.Validate(context.HttpContext.Request))
        {
            _logger.LogWarning("SharedKey Auth FAILED for {Path}", path);

            context.Result = new ContentResult
            {
                StatusCode = 403,
                Content = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                <Error>
                                  <Code>AuthenticationFailed</Code>
                                  <Message>Server failed to authenticate the request. Authorization failed.</Message>
                                </Error>",
                ContentType = "application/xml"
            };
            return;
        }

        await next();
    }
}