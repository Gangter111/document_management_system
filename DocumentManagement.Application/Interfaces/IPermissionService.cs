using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Interfaces;

public interface IPermissionService
{
    bool HasPermission(UserSession? user, string permissionCode);
}