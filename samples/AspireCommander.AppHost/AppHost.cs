var builder = DistributedApplication.CreateBuilder(args);

builder.AddContainer("ubuntu", "ubuntu", "24.04")
    .WithArgs("sleep", "infinity")
    .WithCommander(options =>
    {
        options.Columns = 140;
        options.Rows = 40;
    });

builder.AddContainer("nginx", "nginx", "1.29")
    .WithHttpEndpoint(targetPort: 80)
    .WithCommander(options =>
    {
        options.Path = "/usr/share/nginx/html";
        options.Columns = 140;
        options.Rows = 40;
    });

builder.AddContainer("redis", "redis", "8.2")
    .WithVolume("redis-data", "/data")
    .WithCommander(options =>
    {
        options.Path = "/data";
        options.Columns = 140;
        options.Rows = 40;
    });

builder.AddDockerfile("blazor", "../AspireCommander.Web")
    .WithHttpEndpoint(targetPort: 8080)
    .WithExternalHttpEndpoints()
    .WithCommander(options =>
    {
        options.Path = "/app";
        options.Columns = 140;
        options.Rows = 40;
    });

builder.Build().Run();