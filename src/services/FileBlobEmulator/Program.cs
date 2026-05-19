using FileBlobEmulator.Middleware;
using FileBlobEmulator.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/blobserver-.log",
        rollingInterval: RollingInterval.Day,
        formatter: new Serilog.Formatting.Compact.CompactJsonFormatter()
    )
    .MinimumLevel.Information()
    .CreateLogger();

builder.Services.AddSingleton<SharedKeyValidator>(_ =>
{
    var account = Environment.GetEnvironmentVariable("BLOB_ACCOUNT_NAME")
                  ?? throw new InvalidOperationException("BLOB_ACCOUNT_NAME not set");

    var key = Environment.GetEnvironmentVariable("BLOB_ACCOUNT_KEY")
              ?? throw new InvalidOperationException("BLOB_ACCOUNT_KEY not set");

    return new SharedKeyValidator(account, key);
});


builder.Services.AddScoped<SharedKeyAuthFilter>();
// BlobFileBackend is stateless (no per-request state) so Singleton is correct and avoids
// redundant object allocations and repeated root-directory creation checks.
builder.Services.AddSingleton<BlobFileBackend>();

builder.Host.UseSerilog();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddRouting(option => option.LowercaseUrls = true);

var app = builder.Build();
app.UseMiddleware<SharedKeyAuthMiddleware>();
app.UseSerilogRequestLogging();
app.UseRouting();
app.MapControllers();
// Health check endpoint — used by Docker HEALTHCHECK and Kubernetes liveness probes.
// Authentication is skipped for this path (see SharedKeyAuthMiddleware and SharedKeyAuthFilter).
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.MapOpenApi();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
await app.RunAsync();