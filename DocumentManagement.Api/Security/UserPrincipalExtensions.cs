using System.Security.Claims;

namespace DocumentManagement.Api.Security;

public static class UserPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst("sub")?.Value;

        return long.TryParse(value, out var userId) ? userId : 0;
    }

    public static string GetUsername(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value
               ?? user.Identity?.Name
               ?? "system";
    }

    public static string GetRole(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Role)?.Value
               ?? user.FindFirst("role")?.Value
               ?? string.Empty;
    }

    public static string GetDepartment(this ClaimsPrincipal user)
    {
        return user.FindFirst("department")?.Value
               ?? string.Empty;
    }

    public static bool HasRole(this ClaimsPrincipal user, string role)
    {
        return string.Equals(user.GetRole(), role, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasAnyRole(this ClaimsPrincipal user, params string[] roles)
    {
        var currentRole = user.GetRole();

        return roles.Any(role => string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAdmin(this ClaimsPrincipal user)
    {
        return user.HasRole("Admin");
    }

    public static bool IsManager(this ClaimsPrincipal user)
    {
        return user.HasRole("Manager");
    }

    public static bool IsPublisher(this ClaimsPrincipal user)
    {
        return user.HasRole("Publisher");
    }

    public static bool IsStaff(this ClaimsPrincipal user)
    {
        return user.HasRole("Staff");
    }
}
