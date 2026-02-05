using Quela.Reactive.AspNetCore;
using Quela.Sample.Api.Orchestrations;

var builder = WebApplication.CreateBuilder(args);

// Add Quela services
builder.Services.AddQuelaReactive(options =>
{
    options.SessionTimeout = TimeSpan.FromMinutes(30);
    options.EngineOptions.ExposeDetailedErrors = builder.Environment.IsDevelopment();
});

// Register orchestrations
builder.Services.AddOrchestration(UserRegistrationOrchestration.Build());
builder.Services.AddOrchestration(CheckoutOrchestration.Build());
builder.Services.AddOrchestration(SearchOrchestration.Build());

var app = builder.Build();

// Map Quela endpoints
app.MapQuelaEndpoints("/api/quela");

// Serve static files for the demo
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
