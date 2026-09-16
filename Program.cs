using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ReraGIS.Api.Data;
using ReraGIS.Api.DTOs;
using ReraGIS.Api.Services;
using ReraGIS.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Configuration sanity checks (fail fast at startup rather than on the first request)
// ─────────────────────────────────────────────────────────────────────────────────────────────
IConfigurationSection jwtSection = builder.Configuration.GetSection("Jwt");
string jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured. Set it via appsettings, an environment variable, User Secrets, " +
        "or Key Vault -- see the README section on secret management.");
string? jwtIssuer = jwtSection["Issuer"];
string? jwtAudience = jwtSection["Audience"];

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Dependency injection
// ─────────────────────────────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// Ensure automatic [ApiController] model-validation errors (e.g. a missing required field on
// LoginRequestDto) come back in the same ApiResponse<T> envelope as every other response, instead
// of the framework's default ValidationProblemDetails shape.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        string message = string.Join(
            " ",
            context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m)));

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "The request was invalid.";
        }

        return new BadRequestObjectResult(ApiResponse<object>.FailResponse(message));
    };
});

builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IProjectDetailsService, ProjectDetailsService>();
builder.Services.AddSingleton<JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // In production, force HTTPS metadata (dev may run over plain HTTP behind a local proxy).
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                // Log only the failure reason -- never the token itself.
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtBearer");
                logger.LogWarning("JWT authentication failed: {Reason}", context.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ReraGIS Integration API",
        Version = "v1",
        Description = "Secure Web API exposing RERA project details."
    });

    // Pulls in XML doc comments (enabled via <GenerateDocumentationFile> in the .csproj) so Swagger
    // shows the summaries/response descriptions written on controllers, actions and DTOs.
    string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // "Authorize" button: accepts a raw JWT and sends it as `Authorization: Bearer {token}`.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only the JWT itself here (Swagger adds the \"Bearer \" prefix automatically). " +
                      "Obtain one from POST /api/Auth/login."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Global exception handling: never leak stack traces, connection strings, or secrets to clients.
// Full exception details are logged server-side only.
// ─────────────────────────────────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
            if (exceptionFeature?.Error is { } ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GlobalExceptionHandler");
                logger.LogError(ex, "Unhandled exception while processing {Path}", context.Request.Path);
            }

            var response = ApiResponse<object?>.FailResponse(
                "An unexpected error occurred. Please try again later.");
            await context.Response.WriteAsJsonAsync(response);
        });
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ReraGIS Integration API v1");

        // Hides the collapsible "Schemas" section at the bottom of the Swagger UI page (the list
        // of model definitions like ApiResponse, ProjectDetailsForGISDto, etc.). -1 means "don't
        // render the models section at all"; 0 would still show it collapsed, just not expanded.
        options.DefaultModelsExpandDepth(-1);
    });
}

//app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the implicit Program class to WebApplicationFactory-based integration tests.
public partial class Program;
