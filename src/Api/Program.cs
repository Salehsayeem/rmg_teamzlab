using Rmg.Api;
using Rmg.BuildingBlocks.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationMiddleware>();

app.MapGet("/", () => "Hello World!");
app.MapDevProbeEndpoints();

app.Run();

public partial class Program;
