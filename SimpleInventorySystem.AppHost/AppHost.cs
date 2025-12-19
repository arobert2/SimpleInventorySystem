var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.SimpleInventorySystem>("simpleinventorysystem");

builder.AddProject<Projects.SimpleInventorySystem_Web>("simpleinventorysystem-web");

builder.Build().Run();
