using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Api.Security;
using DocumentManagement.Contracts.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/system")]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly IUserManagementRepository _userManagementRepository;

    public SystemController(IUserManagementRepository userManagementRepository)
    {
        _userManagementRepository = userManagementRepository;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserAdminDto>>> SearchUsers(
        [FromQuery] string? keyword,
        [FromQuery] bool includeInactive = true)
    {
        var permission = RequireAdmin();
        if (permission != null)
        {
            return permission;
        }

        return Ok((await _userManagementRepository.SearchUsersAsync(keyword, includeInactive)).Select(ToDto).ToList());
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles()
    {
        var permission = RequireAdmin();
        if (permission != null)
        {
            return permission;
        }

        return Ok((await _userManagementRepository.GetRolesAsync()).Select(ToDto).ToList());
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserAdminDto>> CreateUser([FromBody] SaveUserRequest request)
    {
        var permission = RequireAdmin();
        if (permission != null)
        {
            return permission;
        }

        try
        {
            var user = await _userManagementRepository.CreateUserAsync(ToModel(request));
            return CreatedAtAction(nameof(SearchUsers), new { keyword = user.Username }, ToDto(user));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("users/{id:long}")]
    public async Task<ActionResult<UserAdminDto>> UpdateUser(
        long id,
        [FromBody] SaveUserRequest request)
    {
        var permission = RequireAdmin();
        if (permission != null)
        {
            return permission;
        }

        try
        {
            if (id == User.GetUserId() && !request.IsActive)
            {
                return BadRequest("Không được tự ngưng kích hoạt tài khoản đang đăng nhập.");
            }

            return Ok(ToDto(await _userManagementRepository.UpdateUserAsync(id, ToModel(request))));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("users/{id:long}")]
    public async Task<IActionResult> DeleteUser(long id)
    {
        var permission = RequireAdmin();
        if (permission != null)
        {
            return permission;
        }

        try
        {
            if (id == User.GetUserId())
            {
                return BadRequest("Không được tự ngưng kích hoạt tài khoản đang đăng nhập.");
            }

            await _userManagementRepository.DeleteUserAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private ActionResult? RequireAdmin()
    {
        return User.IsAdmin()
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, "Chỉ Admin được quản trị hệ thống.");
    }

    private static SaveUserModel ToModel(SaveUserRequest request)
    {
        return new SaveUserModel
        {
            Username = request.Username,
            FullName = request.FullName,
            Department = request.Department,
            RoleId = request.RoleId,
            Password = request.Password,
            IsActive = request.IsActive
        };
    }

    private static UserAdminDto ToDto(UserAdminModel user)
    {
        return new UserAdminDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Department = user.Department,
            RoleId = user.RoleId,
            RoleName = user.RoleName,
            IsActive = user.IsActive
        };
    }

    private static RoleDto ToDto(RoleModel role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name
        };
    }
}
