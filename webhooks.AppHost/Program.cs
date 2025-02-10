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
    .WithReference(db);

//var demoClient = builder.AddProject<Projects.webhooks_DemoClient>("democlient");

var cache = builder.AddRedis("cache");

var sql = builder.AddSqlServer("sql")
                 .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("Data-312adef9-a1d1-447f-9a88-c4836ac84dcb");

builder.AddProject<Projects.webhooks_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
<<<<<<< HEAD
=======
    .WithReference(db)
    .WaitFor(db)
    .WithReference(storageMigrationsClient)
    .WaitFor(storageMigrationsClient)
>>>>>>> 22d6086b7c907916990c96824d703913f6557f90
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
