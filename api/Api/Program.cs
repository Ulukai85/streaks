using Api.Data;
using Api.Features.Challenges;
using Api.Features.Health;
using Api.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Registration phase

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres configuration.")));

builder.Services.AddProblemDetails();

builder.Services.AddScoped<ICurrentUserProvider, DevCurrentUserProvider>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IValidator<CreateChallengeRequest>, CreateChallengeRequestValidator>();

var app = builder.Build();

await app.MigrateAndSeedAsync();

// Pipeline phase

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthEndpoints();
app.MapChallengeEndpoints();

app.Run();
