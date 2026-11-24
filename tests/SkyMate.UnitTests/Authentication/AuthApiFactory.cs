using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SkyMate.API;
using SkyMate.Infrastructure.Persistence;
using SkyMate.Application.Interfaces.Chatbot;
using SkyMate.UnitTests.Chatbot;
using SkyMate.UnitTests.Persistence;
using Microsoft.IdentityModel.Tokens;

namespace SkyMate.UnitTests.Authentication;

/// <summary>Runs the API with an in-memory SQLite database and test-only JWT settings.</summary>
internal sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private readonly SqliteConnection _connection = SqliteTestContext.CreateOpenConnection();

    /// <summary>Configures the test database and token settings.</summary>
    public AuthApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Data Source=auth-test.db");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "SkyMate.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "SkyMate.TestClients");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
        Environment.SetEnvironmentVariable("Jwt__ExpiryMinutes", "60");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();
    }

    /// <summary>Creates a validly signed token that has expired.</summary>
    /// <param name="userId">The test user ID placed in the token.</param>
    /// <returns>The expired JWT string.</returns>
    internal string CreateExpiredToken(int userId)
    {
        var expiredAt = DateTime.UtcNow.AddMinutes(-5);
        var token = new JwtSecurityToken(
            issuer: "SkyMate.Tests",
            audience: "SkyMate.TestClients",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            notBefore: expiredAt.AddMinutes(-5),
            expires: expiredAt,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Add test endpoints and share the in-memory database connection across scopes.
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=auth-test.db",
                ["Jwt:Issuer"] = "SkyMate.Tests",
                ["Jwt:Audience"] = "SkyMate.TestClients",
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:ExpiryMinutes"] = "60",
                ["Cors:AllowedOrigins:0"] = "https://client.example.test",
                ["AssistantAi:MaxHistoryMessages"] = "2"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<IAssistantLlmClient>();
            services.AddSingleton<TestAssistantLlmClient>();
            services.AddSingleton<IAssistantLlmClient>(serviceProvider =>
                serviceProvider.GetRequiredService<TestAssistantLlmClient>());
            services.AddControllers().AddApplicationPart(typeof(AuthTestController).Assembly);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            // Keep the SQLite connection open for the lifetime of the test database.
            _connection.Dispose();
        }
    }
}
