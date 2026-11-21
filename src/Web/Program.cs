using System.Reflection;
using Scalar.AspNetCore;
using Tools.Logging;
using Web;

var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider" ? "Development" : null,
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddLogging(builder.Logging, builder.Configuration);
var modules = HostModules.Create(builder.Environment);
HostModules.RegisterServices(builder.Services, modules);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/", () =>
{
    return "OK";
}); 

modules.MapEndpoints(app, app.Environment);

await app.RunAsync();
