using Microsoft.EntityFrameworkCore;
using webhooks.SharedModels.storage;
using webhooks.SharedModels.clients;
using webhooks.SharedModels.models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("webhooks")));

builder.Services.AddHttpClient<WebhookEventsApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
});

builder.Services.AddSingleton<WebhookConsumer>(sp =>
{
    var webhook = new Webhook(); // TODO: Replace with actual Webhook lookup logic
    var client = sp.GetRequiredService<WebhookEventsApiClient>();
    return new WebhookConsumer(
        name: "DemoConsumer",
        webhook: webhook,
        script: "script.sh",
        httpClient: client
    );
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapControllers();

var consumer = app.Services.GetRequiredService<WebhookConsumer>();
await consumer.StartProcessingLoopAsync();

// Terminate the service once the migration is complete
Environment.Exit(0);