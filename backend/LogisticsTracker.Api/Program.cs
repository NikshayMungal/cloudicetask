using System.Reflection;
using LogisticsTracker.Api.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- Services

builder.Services.AddControllers();

// Swagger / OpenAPI - satisfies the "API documentation" requirement. Enabled in every environment
// so the deployed API can be demonstrated at /swagger.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Logistics Tracker API",
        Version = "v1",
        Description = "Shipment tracking, driver allocation and delivery status management for regional freight dispatchers."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

// Database - PostgreSQL via EF Core. Works with the local compose container and with hosted Neon/Supabase.
var connectionString = ConnectionStringResolver.Resolve(builder.Configuration);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

// CORS - only the configured frontend origin(s) may call the API from a browser.
// Set Cors__AllowedOrigins to a comma-separated list (e.g. your deployed frontend URL).
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// Hosting platforms (Render, etc.) terminate HTTPS at their proxy and forward plain HTTP to the container.
// Trust the X-Forwarded-* headers so the app still knows the original request was HTTPS.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---------------------------------------------------------------- Pipeline

var app = builder.Build();

app.UseForwardedHeaders();

// Basic hardening headers for every response.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    // Tells browsers to use HTTPS only. We deliberately do NOT call UseHttpsRedirection():
    // inside the container the app listens on HTTP and TLS is handled by the platform in front of it.
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Logistics Tracker API v1"));

app.UseCors("Frontend");
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).ExcludeFromDescription();
app.MapGet("/health/db", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "healthy", database = "reachable" })
        : Results.Problem("Database unreachable", statusCode: 503)).ExcludeFromDescription();

// Create the schema on first start. For a production system, replace with EF Core migrations
// (dotnet ef migrations add Initial) and call db.Database.MigrateAsync() instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (app.Configuration.GetValue<bool>("SeedDemoData"))
        await DemoDataSeeder.SeedAsync(db);
}

app.Run();

// Exposed so integration tests (WebApplicationFactory<Program>) can be added later.
public partial class Program;
