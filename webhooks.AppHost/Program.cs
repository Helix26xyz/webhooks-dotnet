var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.webhooks_ApiService>("apiservice");

var sql = builder.AddSqlServer("sql")
                 .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("Data-312adef9-a1d1-447f-9a88-c4836ac84dcb");

builder.AddProject<Projects.webhooks_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(db)
    .WaitFor(db)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
