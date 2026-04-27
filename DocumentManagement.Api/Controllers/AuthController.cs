using System.Collections;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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
            ?? "User";

        return Ok(new LoginResponse
        {
            Success = true,
            Message = "Đăng nhập thành công.",
            UserId = userId,
            Username = username,
            FullName = fullName,
            Role = role
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
}