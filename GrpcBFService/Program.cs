using GrpcBFService;
using GrpcBFService.Bots;
using GrpcBFService.Services;
using Microsoft.Bot.Builder;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();

builder.Services.AddTransient<IBot, EchoBot>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<BotApiService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
