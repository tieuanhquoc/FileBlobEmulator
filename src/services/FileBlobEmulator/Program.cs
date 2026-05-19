using FileBlobEmulator.Middleware;
using FileBlobEmulator.Services;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

#region Serilog

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            formatter: new CompactJsonFormatter(),
            path: "logs/blobserver-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14
        );
});

#endregion

#region Environment Variables

var accountName = Environment.GetEnvironmentVariable("BLOB_ACCOUNT_NAME")
                  ?? throw new InvalidOperationException("BLOB_ACCOUNT_NAME not set");

var accountKey = Environment.GetEnvironmentVariable("BLOB_ACCOUNT_KEY")
                 ?? throw new InvalidOperationException("BLOB_ACCOUNT_KEY not set");

#endregion

#region Services

builder.Services.AddSingleton(new SharedKeyValidator(accountName, accountKey));

builder.Services.AddScoped<SharedKeyAuthFilter>();
builder.Services.AddScoped<BlobFileBackend>();

builder.Services.AddControllers();

builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

#endregion

var app = builder.Build();

#region Middleware Pipeline

app.UseSerilogRequestLogging();

app.UseRouting();

app.UseMiddleware<SharedKeyAuthMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

#endregion

try
{
    Log.Information("Starting Blob Emulator API");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}