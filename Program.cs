using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ReraGIS.Api.Data;
using ReraGIS.Api.DTOs;
using ReraGIS.Api.Services;
using ReraGIS.Api.Services.Interfaces;
using Serilog;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Bootstrap logger: active only while the host itself is starting up, so that a failure during
// configuration binding or DI setup (before the "real" Serilog pipeline below is wired up) still
// gets written to the console/log file instead of being lost silently.
// ─────────────────────────────────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ReraGIS.Api");

    var builder = WebApplication.CreateBuilder(args);

    // Replaces the default logging providers with Serilog. Reads the "Serilog" section from
    // appsettings.json / appsettings.{Environment}.json (so log level can be tuned per
    // environment without a code change/redeploy) and always writes to both the console and a
    // rolling daily file under <app folder>/Logs -- this is what makes the actual exception
    // behind "An unexpected error occurred" visible on the staging server.
    builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: Path.Combine(context.HostingEnvironment.ContentRootPath, "Logs", "log-.txt"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true,
            outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}"));

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Configuration sanity checks (fail fast at startup rather than on the first request)
    // ─────────────────────────────────────────────────────────────────────────────────────────
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

// Logs one line per request (method, path, status code, elapsed ms) to the same console/file
// sinks configured above. Placed before the exception-handling middleware (registered next) so
// it wraps it and reports the final resolved status code -- e.g. the 500 the handler below
// writes for a failed request -- rather than an unhandled-exception status.
app.UseSerilogRequestLogging();

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
    // TEMPORARY (staging debugging only): when true, the 500 response below carries the real
    // exception -- type, message, full stack trace, and SQL error number when applicable -- so it
    // doesn't have to be pulled from the Logs folder for every failed call. This is a genuine
    // information-disclosure risk (stack traces can reveal internal file paths, raw SQL text,
    // etc. to anyone who can reach the endpoint), so set
    // "Diagnostics:IncludeExceptionDetailsInResponse" back to false in appsettings.json once this
    // environment carries real traffic or the investigation is done.
    bool includeExceptionDetails =
        app.Configuration.GetValue("Diagnostics:IncludeExceptionDetailsInResponse", defaultValue: false);

    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            ExceptionDetails? errorDetails = null;

            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
            if (exceptionFeature?.Error is { } ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GlobalExceptionHandler");
                logger.LogError(
                    ex,
                    "Unhandled exception while processing {Method} {Path}{QueryString}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString);

                if (includeExceptionDetails)
                {
                    errorDetails = ExceptionDetails.FromException(ex);
                }
            }

            var response = ApiResponse<object?>.FailResponse(
                "An unexpected error occurred. Please try again later.",
                error: errorDetails);
            await context.Response.WriteAsJsonAsync(response);
        });
    });
}

// Swagger is enabled in every environment (not just Development) so it's reachable on the
// published/IIS-hosted site too -- see README section 3 for the tradeoffs of leaving this on
// in a real production deployment.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("./v1/swagger.json", "Rera GIS API V1");
    options.RoutePrefix = "swagger";
    options.DefaultModelsExpandDepth(-1);
});

//app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown by `dotnet ef`/design-time tooling deliberately stopping the
    // host after building it -- not a real startup failure, so it's excluded from the fatal log.
    Log.Fatal(ex, "ReraGIS.Api terminated unexpectedly during startup");
}
finally
{
    Log.CloseAndFlush();
}

// Exposes the implicit Program class to WebApplicationFactory-based integration tests.
public partial class Program;
