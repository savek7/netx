using Tools.Logging;
using Tools.Modularity;
using Modules.Defaults;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddLogging(builder.Logging, builder.Configuration);

IModule[] modules = [DefaultModule.Create()];
foreach (IModule module in modules)
{
    module.RegisterServices(builder.Services);
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () =>
{
    return "OK";
}); 

foreach (IModule module in modules)
{
    module.MapEndpoints(app, app.Environment);
}

await app.RunAsync();
