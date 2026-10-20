using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StorEase.Api;
using StorEase.Api.Middleware;
using StorEase.Application;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Infrastructure;
using StorEase.Infrastructure.Persistence.Seed;
using StorEase.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var smtp = builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();

builder.Services
    .AddApplication()
    .AddInfrastructure(
        builder.Configuration.GetConnectionString("DefaultConnection"), jwt,
        Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Storage:Root"] ?? "App_Data/uploads"), smtp);

var appOptions = builder.Configuration.GetSection("App").Get<AppOptions>() ?? new AppOptions();
builder.Services.AddSingleton(new AppOptions
{
    FrontendBaseUrl = appOptions.FrontendBaseUrl,
    // Without SMTP the verification link only reaches the API log, so Development skips verification.
    AutoVerifyEmail = builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(smtp.Host),
});
var gateway = builder.Configuration.GetSection("PaymentGateway").Get<PaymentGatewayOptions>() ?? new PaymentGatewayOptions();
builder.Services.AddSingleton(new PaymentGatewayOptions
{
    Secret = gateway.Secret,
    CheckoutBaseUrl = gateway.CheckoutBaseUrl,
    // Simulating the gateway would let a customer mark their own invoice paid, so it never exists outside Development.
    AllowSimulation = builder.Environment.IsDevelopment(),
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
        options.Filters.Add<ActivityLogFilter>();
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

// Off for tools that start the app without wanting side effects (OpenAPI export sets Billing__RunInBackground=false).
if (builder.Configuration.GetValue("Billing:RunInBackground", true))
    builder.Services.AddHostedService<BillingWorker>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names as issued ("sub", "role") instead of the long WS-* URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });

// An endpoint without [Authorize] or [AllowAnonymous] is locked rather than accidentally public.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var bearer = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the token from POST /api/auth/login.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
});

// Owner decision (VS-1341): any origin in every environment. The frontend holds its JWT in memory and sends it
// in the Authorization header, so no credentialed CORS is needed. To restore: WithOrigins("http://localhost:5173").
const string AnyOriginCors = "AnyOrigin";
builder.Services.AddCors(options => options.AddPolicy(AnyOriginCors, policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// `dotnet run -- --seed-only` (used by scripts/reset-demo.ps1) loads the demo data and exits.
if (app.Environment.IsDevelopment() || args.Contains("--seed-only"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
    if (args.Contains("--seed-only"))
        return;
}

// Checked after Build() so `dotnet ef`, which stops at Build(), still works on a machine without a key.
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. Set it in BE/StorEase.Api/appsettings.Development.json.");

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(AnyOriginCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
