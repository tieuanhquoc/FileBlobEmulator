using FileBlobEmulator.Middleware;
using FileBlobEmulator.Services;
using Microsoft.AspNetCore.Http;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Missing Authorization is rejected", MissingAuthorizationIsRejected),
    ("Non-SharedKey Authorization is rejected", NonSharedKeyAuthorizationIsRejected),
    ("Swagger path bypasses authentication", SwaggerPathBypassesAuthentication),
};

var failed = false;

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed = true;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

return failed ? 1 : 0;

static async Task MissingAuthorizationIsRejected()
{
    var calledNext = false;
    var context = CreateContext("/blob1/container");
    var middleware = CreateMiddleware(() => calledNext = true);

    await middleware.InvokeAsync(context, CreateValidator());

    AssertFalse(calledNext, "request reached next middleware");
    AssertEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode, "status code");
}

static async Task NonSharedKeyAuthorizationIsRejected()
{
    var calledNext = false;
    var context = CreateContext("/blob1/container");
    context.Request.Headers.Authorization = "Bearer token";
    var middleware = CreateMiddleware(() => calledNext = true);

    await middleware.InvokeAsync(context, CreateValidator());

    AssertFalse(calledNext, "request reached next middleware");
    AssertEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode, "status code");
}

static async Task SwaggerPathBypassesAuthentication()
{
    var calledNext = false;
    var context = CreateContext("/swagger/index.html");
    var middleware = CreateMiddleware(() => calledNext = true);

    await middleware.InvokeAsync(context, CreateValidator());

    AssertTrue(calledNext, "swagger request did not reach next middleware");
    AssertEqual(StatusCodes.Status200OK, context.Response.StatusCode, "status code");
}

static DefaultHttpContext CreateContext(string path)
{
    var context = new DefaultHttpContext();
    context.Request.Path = path;
    context.Response.Body = new MemoryStream();
    return context;
}

static SharedKeyAuthMiddleware CreateMiddleware(Action onNext)
{
    return new SharedKeyAuthMiddleware(_ =>
    {
        onNext();
        return Task.CompletedTask;
    });
}

static SharedKeyValidator CreateValidator()
{
    return new SharedKeyValidator(
        "blob1",
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ11234560uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertFalse(bool condition, string message)
{
    AssertTrue(!condition, message);
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}
