using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Sessions.Repo.Infrastructure.Services;
using Sessions.Service.Filters;
using Sessions.Service.Initializations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<ApiKeyAuthorizationFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.RegisterApplicationServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<VeriClaim.Logging.Infrastructure.Services.ILogger>();
    await logger.ErrorAsync($"Sessions API {context.Request.Method} {context.Request.Path}{Environment.NewLine}-->{exception?.Message}{Environment.NewLine}-->{exception?.StackTrace}");

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.Services.GetRequiredService<ISessionRepoService>().EnsureIndexesAsync();

app.UseHttpsRedirection();
app.MapGet("/health", () => Results.Ok("Healthy"));
app.MapControllers();

app.Run();

public partial class Program
{
}
