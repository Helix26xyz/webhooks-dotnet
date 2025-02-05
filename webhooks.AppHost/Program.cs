var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.webhooks_ApiService>("apiservice");
var demoClient = builder.AddProject<Projects.webhooks_DemoClient>("democlient");

builder.AddProject<Projects.webhooks_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WithReference(demoClient)
    .WaitFor(apiService);

builder.Build().Run();
