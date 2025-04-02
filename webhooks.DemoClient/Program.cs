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

var baseApiURL = builder.Configuration["BaseApiUrl"];
var name = builder.Configuration["Name"];
var script = builder.Configuration["Script"];
var webhook = builder.Configuration["Webhook"];

if (string.IsNullOrEmpty(baseApiURL))
{
    throw new ArgumentNullException("BaseApiUrl", "BaseApiUrl is not set in the configuration.");
}

builder.Services.AddHttpClient<WebhookEventsApiClient>(client =>
{
    client.BaseAddress = new(baseApiURL);
});
builder.Services.AddHttpClient<WebhookApiClient>(client =>
{
    client.BaseAddress = new(baseApiURL);
});

builder.Services.AddSingleton<WebhookConsumer>(sp =>
{
    var webhookEventClient = sp.GetRequiredService<WebhookEventsApiClient>();
    var webhookClient = sp.GetRequiredService<WebhookApiClient>();
    var webhookConsumer = WebhookConsumer.CreateAsync(
        webhookEventHttpClient: webhookEventClient,
        webhookHttpClient: webhookClient,
        name: name,
        webhook: webhook,
        script: script
    ).GetAwaiter().GetResult();
    return webhookConsumer;
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