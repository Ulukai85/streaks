using Api.Data;
using Api.Features.Health;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Registration phase

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres configuration.")));

builder.Services.AddProblemDetails();

var app = builder.Build();

// Pipeline phase

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthEndpoints();

app.Run();

// Top-level statements compile to an internal Program class; WebApplicationFactory<Program>
// (used by Api.Tests for endpoint tests, see Stage 2) needs a type it can see from another
// assembly, so this partial declaration widens it to public.
public partial class Program;
