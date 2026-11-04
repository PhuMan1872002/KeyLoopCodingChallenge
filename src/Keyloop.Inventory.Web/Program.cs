using System.Globalization;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Application.Exports;
using Keyloop.Inventory.Infrastructure;
using Keyloop.Inventory.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "keyloop-inventory-dashboard",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource([.. InfrastructureRegistration.TraceSources])
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter(InfrastructureRegistration.MeterName)
        .AddConsoleExporter());

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInventoryInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/inventory.csv", async (
    HttpContext http,
    InventoryQueryService inventory,
    string? dealership,
    string? make,
    string? model,
    string? bucket,
    string? q,
    string? sort,
    bool? agingOnly,
    CancellationToken cancellationToken) =>
{
    var filter = new VehicleFilter
    {
        DealershipId = dealership,
        Make = make,
        Model = model,
        Search = q,
        AgeBucket = Enum.TryParse<AgeBucket>(bucket, out var parsedBucket) ? parsedBucket : null,
        AgingOnly = agingOnly ?? false,
        Sort = Enum.TryParse<SortOrder>(sort, out var parsedSort) ? parsedSort : SortOrder.OldestFirst,
    };

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var rows = await inventory.GetVehiclesAsync(filter, today, cancellationToken);

    http.Response.Headers.ContentDisposition = $"attachment; filename=inventory-{today:yyyy-MM-dd}.csv";

    return Results.Text(InventoryCsv.Render(rows, today), "text/csv");
});

app.Run();

public partial class Program;
