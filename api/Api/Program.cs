using Api.Data;
using Api.Features.Auth;
using Api.Features.Challenges;
using Api.Features.Completions;
using Api.Features.Dashboard;
using Api.Features.Health;
using Api.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Registration phase

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres configuration.")));

builder.Services.AddExceptionHandler<UniqueConstraintExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddAppDataProtection(builder.Environment);
builder.Services.AddAppAuthentication();
builder.Services.AddAppRateLimiting(builder.Environment);

builder.Services.AddScoped<ICurrentUserProvider, HttpCurrentUserProvider>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IValidator<CreateChallengeRequest>, CreateChallengeRequestValidator>();
builder.Services.AddScoped<IValidator<CompleteChallengeRequest>, CompleteChallengeRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<RefreshTokenIssuer>();

var app = builder.Build();

await app.MigrateAndSeedAsync();

// Pipeline phase

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapChallengeEndpoints();
app.MapCompletionEndpoints();
app.MapDashboardEndpoints();

app.Run();
