using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using SkyMate.Application.Interfaces.Chatbot;
using SkyMate.Application.Interfaces.Authentication;
using SkyMate.Application.Interfaces.Persistence;
using SkyMate.Application.Interfaces.Travel;
using SkyMate.Infrastructure.Authentication;
using SkyMate.Infrastructure.AI;
using SkyMate.Infrastructure.Identity;
using SkyMate.Infrastructure.Persistence;
using SkyMate.Infrastructure.Persistence.Repositories;
using SkyMate.Infrastructure.Services;

namespace SkyMate.Infrastructure;

/// <summary>Registers the Infrastructure services used by the application.</summary>
public static class DependencyInjection
{
    /// <summary>Registers persistence, Identity, and application service implementations.</summary>
    /// <param name="services">The ASP.NET Core dependency injection container.</param>
    /// <param name="configuration">The application's configuration sources.</param>
    /// <returns>The same service collection for chained registration.</returns>
    /// <exception cref="InvalidOperationException">The database connection string is missing.</exception>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is missing. Set it with .NET User Secrets for local development.");
        }

        // DbContext is scoped so each request gets its own context instance.
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddMemoryCache();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => options.IsValid,
                "JWT configuration is invalid. Configure Jwt:Issuer, Jwt:Audience, a Jwt:SigningKey of at least 32 UTF-8 bytes, and a positive Jwt:ExpiryMinutes.")
            .ValidateOnStart();
        services.AddOptions<AssistantAiOptions>()
            .Bind(configuration.GetSection(AssistantAiOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model) &&
                                options.TimeoutSeconds > 0 &&
                                options.MaxHistoryMessages > 0,
                "AssistantAi configuration requires a model, positive timeout, and positive history limit.")
            .ValidateOnStart();

        // Identity manages accounts, password hashes, and lockout; EF Core stores its data.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;

                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<AppDbContext>();

        // DbContext-dependent services are scoped; the system clock is safe to share.
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITripReadService, TripReadService>();
        services.AddScoped<KnowledgeBaseProvider>();
        services.AddScoped<KnowledgeBaseSeeder>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddScoped<IChatbotConversationService, ChatbotConversationService>();
        services.AddSingleton<IAssistantPromptBuilder, AssistantPromptBuilder>();
        services.AddScoped<IAssistantService, AssistantService>();
        return services;
    }
}
