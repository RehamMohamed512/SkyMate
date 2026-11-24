namespace SkyMate.Infrastructure.Authentication;

/// <summary>JWT settings read from the application's Jwt configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; }

    /// <summary>Checks that required values exist and meet the token security requirements.</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(Audience) &&
        !string.IsNullOrWhiteSpace(SigningKey) &&
        System.Text.Encoding.UTF8.GetByteCount(SigningKey) >= 32 &&
        ExpiryMinutes > 0;

    /// <summary>Validates the JWT settings without including the signing key in errors.</summary>
    /// <exception cref="InvalidOperationException">The settings are missing or invalid.</exception>
    public void Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Issuer)) errors.Add("Jwt:Issuer is required.");
        if (string.IsNullOrWhiteSpace(Audience)) errors.Add("Jwt:Audience is required.");
        if (string.IsNullOrWhiteSpace(SigningKey) || System.Text.Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            // Do not include the signing key in the error message.
            errors.Add("Jwt:SigningKey is required and must contain at least 32 UTF-8 bytes.");
        }
        if (ExpiryMinutes <= 0) errors.Add("Jwt:ExpiryMinutes must be greater than zero.");

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Invalid JWT configuration: {string.Join(" ", errors)} " +
                "Store Jwt:SigningKey in User Secrets for local development or provide it as Jwt__SigningKey in the process environment.");
        }
    }
}
