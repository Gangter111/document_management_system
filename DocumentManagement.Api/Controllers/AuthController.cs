using System.Collections;
using DocumentManagement.Api.Services;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.Auth;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly JwtService _jwtService;
    private readonly IAuditLogRepository _auditLogRepository;

    public AuthController(IAuthService authService, JwtService jwtService, IAuditLogRepository auditLogRepository)
    {
        _authService = authService;
        _jwtService = jwtService;
        _auditLogRepository = auditLogRepository;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new LoginResponse
            {
                Success = false,
                Message = "Tên đăng nhập không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponse
            {
                Success = false,
                Message = "Mật khẩu không được để trống."
            });
        }

        var userSession = await _authService.LoginAsync(request.Username, request.Password);

        if (userSession == null)
        {
            await WriteLoginFailureAuditAsync(request.Username);
            return Unauthorized(new LoginResponse
            {
                Success = false,
                Message = "Tên đăng nhập hoặc mật khẩu không đúng."
            });
        }

        var userId = GetLongValue(userSession, "UserId")
            ?? GetLongValue(userSession, "Id")
            ?? 0;

        var username = GetStringValue(userSession, "Username")
            ?? request.Username;

        var fullName = GetStringValue(userSession, "DisplayName")
            ?? GetStringValue(userSession, "FullName")
            ?? username;

        var role = GetRoleValue(userSession)
            ?? GetStringValue(userSession, "Role")
            ?? "STAFF";

        var department = GetStringValue(userSession, "Department") ?? string.Empty;

        var token = _jwtService.GenerateToken(userId, username, fullName, role, department);

        return Ok(new LoginResponse
        {
            Success = true,
            Message = "Đăng nhập thành công.",
            UserId = userId,
            Username = username,
            FullName = fullName,
            Role = role,
            Department = department,
            Token = token
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new LoginResponse
            {
                Success = false,
                Message = "Tên đăng nhập không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new LoginResponse
            {
                Success = false,
                Message = "Mật khẩu phải có ít nhất 6 ký tự."
            });
        }

        var userSession = await _authService.RegisterAsync(
            request.Username,
            request.Password,
            request.FullName,
            request.Department);

        if (userSession == null)
        {
            return BadRequest(new LoginResponse
            {
                Success = false,
                Message = "Không thể đăng ký. Tên đăng nhập có thể đã tồn tại."
            });
        }

        var userId = GetLongValue(userSession, "UserId")
            ?? GetLongValue(userSession, "Id")
            ?? 0;
        var username = GetStringValue(userSession, "Username") ?? request.Username;
        var fullName = GetStringValue(userSession, "DisplayName")
            ?? GetStringValue(userSession, "FullName")
            ?? username;
        var role = GetRoleValue(userSession)
            ?? GetStringValue(userSession, "Role")
            ?? "STAFF";
        var department = GetStringValue(userSession, "Department") ?? string.Empty;
        var token = _jwtService.GenerateToken(userId, username, fullName, role, department);

        return Ok(new LoginResponse
        {
            Success = true,
            Message = "Đăng ký tài khoản thành công.",
            UserId = userId,
            Username = username,
            FullName = fullName,
            Role = role,
            Department = department,
            Token = token
        });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (request.UserId <= 0)
        {
            return BadRequest("UserId không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest("Mật khẩu mới không được để trống.");
        }

        var success = await _authService.ChangePasswordAsync(
            request.UserId,
            request.NewPassword);

        if (!success)
        {
            return BadRequest("Không thể đổi mật khẩu.");
        }

        return NoContent();
    }

    private static string? GetStringValue(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);

        if (property == null)
        {
            return null;
        }

        var value = property.GetValue(source);

        return value?.ToString();
    }

    private static long? GetLongValue(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);

        if (property == null)
        {
            return null;
        }

        var value = property.GetValue(source);

        if (value == null)
        {
            return null;
        }

        if (value is long longValue)
        {
            return longValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return long.TryParse(value.ToString(), out var result)
            ? result
            : null;
    }

    private static string? GetRoleValue(object source)
    {
        var rolesProperty = source.GetType().GetProperty("Roles");

        if (rolesProperty == null)
        {
            return null;
        }

        var value = rolesProperty.GetValue(source);

        if (value is string roleText)
        {
            return roleText;
        }

        if (value is IEnumerable roles)
        {
            foreach (var role in roles)
            {
                var text = role?.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private async Task WriteLoginFailureAuditAsync(string username)
    {
        await _auditLogRepository.AddAsync(new AuditLog
        {
            EntityName = "Auth",
            EntityId = 0,
            Action = "LOGIN_FAILURE",
            ChangedColumns = "FAILURE",
            NewValues = string.IsNullOrWhiteSpace(username) ? null : username.Trim(),
            Username = "anonymous",
            CreatedAt = DateTime.UtcNow
        });
    }
}
