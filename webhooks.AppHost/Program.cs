var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.webhooks_ApiService>("apiservice");
var demoClient = builder.AddProject<Projects.webhooks_DemoClient>("democlient");
var storageMigrationsClient = builder.AddProject<Projects.webhooks_StorageMigrations>("storageMigrations");

builder.AddProject<Projects.webhooks_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(storageMigrationsClient)
    .WaitFor(storageMigrationsClient)
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(demoClient)
    .WaitFor(demoClient);

builder.Build().Run();
