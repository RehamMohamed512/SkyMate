using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SkyMate.API.DTOs.Auth;
using SkyMate.Infrastructure.Identity;

namespace SkyMate.UnitTests.Authentication;

/// <summary>HTTP tests for registration, sign-in, JWTs, roles, and Swagger.</summary>
public sealed class AuthenticationEndpointTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Creates an HTTP client for the test server.</summary>
    public AuthenticationEndpointTests() => _client = _factory.CreateClient();

    /// <summary>Checks that registration stores a password hash and assigns the User role.</summary>
    [Fact]
    public async Task RegisterCreatesHashedUserAndAssignsUserRole()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", RegisterBody("register@example.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registered = await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions);
        Assert.NotNull(registered);
        Assert.Equal("register@example.com", registered.Email);
        Assert.Equal(new[] { "User" }, registered.Roles);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(registered.Email);
        Assert.NotNull(user);
        Assert.NotEqual("SkyMatePass1", user.PasswordHash);
        Assert.True(await userManager.CheckPasswordAsync(user, "SkyMatePass1"));
        Assert.True(user.LockoutEnabled);
    }

    /// <summary>Checks that role seeding can run repeatedly without creating duplicates.</summary>
    [Fact]
    public async Task RoleSeedingIsIdempotent()
    {
        await IdentityRoleSeeder.SeedAsync(_factory.Services);

        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        Assert.True(await roleManager.RoleExistsAsync("User"));
        Assert.True(await roleManager.RoleExistsAsync("Admin"));
        Assert.Equal(2, roleManager.Roles.Count());
    }

    /// <summary>Checks that registering an existing email returns Conflict.</summary>
    [Fact]
    public async Task RegisterReturnsConflictForDuplicateEmail()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await _client.PostAsJsonAsync("/api/v1/auth/register", RegisterBody("duplicate@example.com"))).StatusCode);

        var duplicateResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/register", RegisterBody("DUPLICATE@example.com"));

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var body = await duplicateResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Checks that invalid registration data returns a validation error.</summary>
    [Fact]
    public async Task RegisterRejectsInvalidData()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "",
            lastName = "Tester",
            email = "not-an-email",
            password = "weak",
            confirmPassword = "different"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Checks the JWT signature, claims, and expiration returned by sign-in.</summary>
    [Fact]
    public async Task LoginReturnsSignedJwtWithConfiguredClaimsAndExpiry()
    {
        await RegisterAsync("login@example.com");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "login@example.com",
            password = "SkyMatePass1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.NotNull(login);
        Assert.Equal("Bearer", login.TokenType);
        Assert.Equal(3600, login.ExpiresInSeconds);
        Assert.Equal(new[] { "User" }, login.User.Roles);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        Assert.Equal("HS256", jwt.Header.Alg);
        Assert.Equal(login.User.Id.ToString(), jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("login@example.com", jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("User", jwt.Claims.Single(claim => claim.Type == "role").Value);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value));
        Assert.Equal(TimeSpan.FromMinutes(60), jwt.ValidTo - jwt.ValidFrom);
    }

    /// <summary>Checks that unknown emails and wrong passwords return the same error.</summary>
    [Fact]
    public async Task LoginUsesGenericUnauthorizedResponseForWrongPasswordAndUnknownEmail()
    {
        await RegisterAsync("known@example.com");
        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "known@example.com",
            password = "WrongPassword1"
        });
        var unknownEmail = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "unknown@example.com",
            password = "WrongPassword1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var wrongText = await wrongPassword.Content.ReadAsStringAsync();
        var unknownText = await unknownEmail.Content.ReadAsStringAsync();
        using var wrongBody = JsonDocument.Parse(wrongText);
        using var unknownBody = JsonDocument.Parse(unknownText);
        Assert.Equal(wrongBody.RootElement.GetProperty("title").GetString(),
            unknownBody.RootElement.GetProperty("title").GetString());
        Assert.Equal(401, wrongBody.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("known@example.com", wrongText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Checks that five failed sign-in attempts lock the account.</summary>
    [Fact]
    public async Task LockoutStartsAfterFiveFailedAttemptsAndReturnsGenericUnauthorized()
    {
        await RegisterAsync("lockout@example.com");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = "lockout@example.com",
                password = "IncorrectPassword1"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var correctPassword = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "lockout@example.com",
            password = "SkyMatePass1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, correctPassword.StatusCode);
    }

    /// <summary>Checks that the current-user endpoint requires a token and returns its user.</summary>
    [Fact]
    public async Task MeRequiresBearerTokenAndReturnsCurrentUser()
    {
        var unauthorized = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        await RegisterAsync("me@example.com");
        var login = await LoginAsync("me@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await _client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions);
        Assert.NotNull(profile);
        Assert.Equal("me@example.com", profile.Email);
        Assert.Equal(new[] { "User" }, profile.Roles);
    }

    /// <summary>Checks authentication and User/Admin role requirements on protected endpoints.</summary>
    [Fact]
    public async Task ProtectedEndpointsEnforceAuthenticationAndAdminRole()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/test-auth/protected")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/test-auth/admin")).StatusCode);

        await RegisterAsync("user@example.com");
        var regularUser = await LoginAsync("user@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regularUser.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/test-auth/protected")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/test-auth/admin")).StatusCode);

        var adminToken = await CreateAndLoginAdminAsync("admin@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/test-auth/admin")).StatusCode);
    }

    /// <summary>Checks that Swagger documents Bearer security for protected operations.</summary>
    [Fact]
    public async Task SwaggerPublishesBearerSchemeAndAuthEndpoints()
    {
        var uiResponse = await _client.GetAsync("/swagger/index.html");
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.True(root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
        var paths = root.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/auth/register", out _));
        Assert.True(paths.TryGetProperty("/api/v1/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/v1/auth/me", out _));
        Assert.Contains("security", paths.GetProperty("/api/v1/auth/me").GetProperty("get").EnumerateObject()
            .Select(property => property.Name));
        Assert.False(paths.GetProperty("/api/v1/auth/login").GetProperty("post").TryGetProperty("security", out _));
    }

    private async Task RegisterAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", RegisterBody(email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<LoginResponse> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "SkyMatePass1"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
    }

    private async Task<string> CreateAndLoginAdminAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "System",
            LastName = "Administrator",
            LockoutEnabled = true
        };
        var created = await userManager.CreateAsync(user, "SkyMatePass1");
        Assert.True(created.Succeeded, string.Join(";", created.Errors.Select(error => error.Code)));
        var assigned = await userManager.AddToRoleAsync(user, "Admin");
        Assert.True(assigned.Succeeded, string.Join(";", assigned.Errors.Select(error => error.Code)));

        return (await LoginAsync(email)).AccessToken;
    }

    private static object RegisterBody(string email) => new
    {
        firstName = "Sky",
        lastName = "Mate",
        email,
        password = "SkyMatePass1",
        confirmPassword = "SkyMatePass1"
    };

    /// <summary>Disposes the HTTP client and test server.</summary>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
