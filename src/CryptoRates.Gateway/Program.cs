using CryptoRates.Gateway.DependencyInjection;
using CryptoRates.Gateway.Endpoints;
using CryptoRates.Gateway.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddErrorHandling();
builder.Services.AddGatewayRateLimiting(builder.Configuration);
builder.Services.AddRates(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

const string corsPolicyName = "AllowFrontend"; 

builder.Services.AddCors(options => options.AddPolicy(corsPolicyName, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.RegisterScalar();

app.UseCors(corsPolicyName);   
app.UseRateLimiter();

app.UseAuthorization();
app.MapHub<RatesHub>("/hubs/rates").RequireCors(corsPolicyName);

app.MapRatesEndpoints();
app.MapVersionEndpoints();

app.Run();

public partial class Program;