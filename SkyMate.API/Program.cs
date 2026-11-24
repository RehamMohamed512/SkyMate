
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SkyMate.API.Swagger;
using SkyMate.API.ExceptionHandling;
using SkyMate.Application.Interfaces.Chatbot;
using SkyMate.Infrastructure;
using SkyMate.Infrastructure.AI;
using SkyMate.Infrastructure.Authentication;
using SkyMate.Infrastructure.Identity;

namespace SkyMate.API;

/// <summary>Configures and starts the SkyMate API and its middleware pipeline.</summary>
public partial class Program
{
    /// <summary>Builds the web host, configures services, and starts the API.</summary>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (builder.Environment.IsDevelopment() &&
            string.IsNullOrWhiteSpace(builder.Configuration["Jwt:SigningKey"]))
        {
            // Load local secrets when no signing key is configured, keeping it out of source control.
            builder.Configuration.AddUserSecrets<Program>(optional: true);
        }

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration section 'Jwt' is missing.");
        try
        {
            jwtOptions.Validate();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"{exception.Message} Current environment: '{builder.Environment.EnvironmentName}'. " +
                "For local development, run with the 'http' or 'https' launch profile so User Secrets are loaded. " +
                "For other environments, set Jwt__SigningKey as an environment variable.",
                exception);
        }

        builder.Services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<AssistantExceptionHandler>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { code = "assistant_rate_limited" },
                    cancellationToken);
            };
            options.AddPolicy("assistant-message", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue("sub") ??
                    context.Connection.RemoteIpAddress?.ToString() ??
                    "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
        builder.Services.AddCors(options => options.AddPolicy("ConfiguredCors", policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            }
        }));

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHttpClient<IAssistantLlmClient, GeminiAssistantLlmClient>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            // GeminiAssistantLlmClient applies a per-request timeout that can distinguish
            // provider timeouts from caller cancellation.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        // Bearer middleware validates the token before a controller handles the request.
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    // Allow a small clock difference while still rejecting expired tokens.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });
        builder.Services.AddAuthorization();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "SkyMate API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter a JWT access token. Swagger adds the Bearer prefix automatically."
            });
            options.OperationFilter<AuthorizeOperationFilter>();
        });

        var app = builder.Build();

        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseCors("ConfiguredCors");
        // Authentication must run before authorization so policies can inspect the user.
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapGet("/", () => Results.Redirect("/swagger"))
                .ExcludeFromDescription();
        }

        app.MapControllers();
        // Seed required roles before accepting requests and starting hosted services.
        await IdentityRoleSeeder.SeedAsync(app.Services);

        // Seed the embedded AI-team knowledge base once, preserving any existing/admin-edited rows.
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var knowledgeBaseSeeder = scope.ServiceProvider.GetRequiredService<KnowledgeBaseSeeder>();
            var seedResult = await knowledgeBaseSeeder.SeedDefaultsAsync();
            app.Logger.LogInformation(
                "Assistant knowledge-base seed check completed: added {Added} defaults; {AlreadyPresent} were already present.",
                seedResult.Added,
                seedResult.AlreadyPresent);
        }

        await app.RunAsync();
    }
}
