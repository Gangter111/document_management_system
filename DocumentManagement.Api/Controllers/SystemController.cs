using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Api.Security;
using DocumentManagement.Contracts.System;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/system")]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly IUserManagementRepository _userManagementRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public SystemController(
        IUserManagementRepository userManagementRepository,
        IAuditLogRepository auditLogRepository)
    {
        _userManagementRepository = userManagementRepository;
        _auditLogRepository = auditLogRepository;
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
            await AddUserAuditAsync(user.Id, "USER_CREATE", "CREATED", user);
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

            var user = await _userManagementRepository.UpdateUserAsync(id, ToModel(request));
            await AddUserAuditAsync(user.Id, "USER_UPDATE", "UPDATED", user);
            return Ok(ToDto(user));
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
            await AddUserAuditAsync(id, "USER_DELETE", "IsActive", null);
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

    private Task AddUserAuditAsync(
        long userId,
        string action,
        string changedColumns,
        UserAdminModel? user)
    {
        return _auditLogRepository.AddAsync(new AuditLog
        {
            EntityName = "User",
            EntityId = userId,
            Action = action,
            ChangedColumns = changedColumns,
            NewValues = user == null
                ? "User deactivated"
                : $"Username={user.Username};Role={user.RoleName};IsActive={user.IsActive}",
            Username = User.GetUsername(),
            CreatedAt = DateTime.UtcNow
        });
    }
}
