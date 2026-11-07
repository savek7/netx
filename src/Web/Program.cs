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

ModuleConfiguration modules = new([
    ModuleDeclaration.Enabled(DefaultModule.Create())
]);
modules.RegisterServices(builder.Services);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () =>
{
    return "OK";
}); 

modules.MapEndpoints(app, app.Environment);

await app.RunAsync();
