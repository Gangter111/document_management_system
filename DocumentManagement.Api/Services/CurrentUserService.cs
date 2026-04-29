using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using DocumentManagement.Application.Interfaces;

namespace DocumentManagement.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Username => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role)
                           ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("role");
    public string? Department => _httpContextAccessor.HttpContext?.User?.FindFirstValue("department");
}
