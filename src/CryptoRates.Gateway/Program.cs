using CryptoRates.Gateway.DependencyInjection;
using CryptoRates.Gateway.Endpoints;
using CryptoRates.Gateway.Hubs;

const string WebCorsPolicy = "Web";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddErrorHandling();
builder.Services.AddRates(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

// Browser clients (Vue) need their origin allowed. SignalR also needs credentials,
// which only works with an explicit origin list (not "*").
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(WebCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.RegisterScalar();

app.UseCors(WebCorsPolicy);

app.MapHub<RatesHub>("/hubs/rates");

app.MapRatesEndpoints();
app.MapVersionEndpoints();

app.Run();

// Makes the entry point visible to WebApplicationFactory in the test project.
public partial class Program;