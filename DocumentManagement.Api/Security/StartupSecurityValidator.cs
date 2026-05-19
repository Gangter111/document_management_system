using System.Text.RegularExpressions;

namespace DocumentManagement.Api.Security;

public static class StartupSecurityValidator
{
    private static readonly string[] PlaceholderMarkers =
    {
        "CHANGE_THIS",
        "DEV_ONLY",
        "PLACEHOLDER",
        "DEFAULT",
        "SECRET_KEY"
    };

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        var jwtSecret = configuration["Jwt:Secret"];
        if (environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(jwtSecret))
                throw new InvalidOperationException("Development JWT secret is missing. Configure Jwt:Secret.");

            return;
        }

        ValidateProductionJwtSecret(jwtSecret);
        ValidateProductionAdminSeed(configuration);
    }

    public static void ValidateProductionJwtSecret(string? jwtSecret)
    {
        if (string.IsNullOrWhiteSpace(jwtSecret))
            throw new InvalidOperationException("Production JWT secret is missing. Set Jwt:Secret from a secure external configuration source.");

        if (jwtSecret.Length < 48)
            throw new InvalidOperationException("Production JWT secret is too short. Use at least 48 high-entropy characters.");

        if (PlaceholderMarkers.Any(marker => jwtSecret.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Production JWT secret is a placeholder. Replace Jwt:Secret with a high-entropy secret from secure configuration.");

        if (!Regex.IsMatch(jwtSecret, "[a-z]") ||
            !Regex.IsMatch(jwtSecret, "[A-Z]") ||
            !Regex.IsMatch(jwtSecret, "[0-9]") ||
            !Regex.IsMatch(jwtSecret, "[^a-zA-Z0-9]"))
        {
            throw new InvalidOperationException("Production JWT secret is weak. Use mixed case, digits, and symbols.");
        }
    }

    private static void ValidateProductionAdminSeed(IConfiguration configuration)
    {
        var password = configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
            return;

        if (password.Length < 16 || PlaceholderMarkers.Any(marker => password.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Production AdminSeed:Password is weak or placeholder. Remove it or provide a strong one through secure external configuration.");
    }
}
