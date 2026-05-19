using DocumentManagement.Api.Security;
using Xunit;

namespace DocumentManagement.Tests.Security;

public sealed class StartupSecurityValidatorTests
{
    [Fact]
    public void ValidateProductionJwtSecret_RejectsMissingSecret()
    {
        Assert.Throws<InvalidOperationException>(() => StartupSecurityValidator.ValidateProductionJwtSecret(null));
    }

    [Fact]
    public void ValidateProductionJwtSecret_RejectsPlaceholderSecret()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StartupSecurityValidator.ValidateProductionJwtSecret("CHANGE_THIS_TO_A_LONG_SECURE_SECRET_KEY_32_CHARS_MIN_2026!"));
    }

    [Fact]
    public void ValidateProductionJwtSecret_AllowsStrongSecret()
    {
        StartupSecurityValidator.ValidateProductionJwtSecret("Pr0duction!Jwt#Secret$With%Enough^Entropy&Length2026");
    }
}
