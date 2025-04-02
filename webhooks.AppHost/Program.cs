using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);


var sql = builder.AddSqlServer("DefaultConnection");

var db = sql.AddDatabase("webhooks");

var storageMigrationsClient = builder.AddProject<Projects.webhooks_StorageMigrations>("storageMigrations")
    .WithReference(db)
    .WaitFor(db);

var apiService = builder.AddProject<Projects.webhooks_ApiService>("apiservice")
    .WithReference(db)
    .WithExternalHttpEndpoints();

var cache = builder.AddRedis("cache");

builder.AddProject<Projects.webhooks_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddProject<Projects.webhooks_DemoClient>("democlient")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
