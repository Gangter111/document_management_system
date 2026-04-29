using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Security;

public sealed class PermissionService : IPermissionService
{
    public bool HasPermission(UserSession? user, string permissionCode)
    {
        if (user == null || string.IsNullOrWhiteSpace(permissionCode))
            return false;

        var roleName = user.Roles.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(roleName))
            return false;

        return GetPermissions(roleName)
            .Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> GetPermissions(string roleName)
    {
        // ================= ADMIN =================
        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PermissionCodes.DashboardView,

                PermissionCodes.DocumentView,
                PermissionCodes.DocumentCreate,
                PermissionCodes.DocumentEdit,
                PermissionCodes.DocumentDelete,

                // FULL WORKFLOW
                PermissionCodes.DocumentSubmit,
                PermissionCodes.DocumentApprove,
                PermissionCodes.DocumentReject,

                PermissionCodes.TaskView,
                PermissionCodes.TaskManage,

                PermissionCodes.ReportView,
                PermissionCodes.SettingsView,

                PermissionCodes.BackupCreate,
                PermissionCodes.BackupRestore
            };
        }

        // ================= MANAGER =================
        if (string.Equals(roleName, "Manager", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PermissionCodes.DashboardView,

                PermissionCodes.DocumentView,
                PermissionCodes.DocumentEdit,

                // CHỈ DUYỆT / TỪ CHỐI
                PermissionCodes.DocumentApprove,
                PermissionCodes.DocumentReject,

                PermissionCodes.TaskView,
                PermissionCodes.TaskManage,

                PermissionCodes.ReportView
            };
        }

        // ================= PUBLISHER =================
        if (string.Equals(roleName, "Publisher", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PermissionCodes.DashboardView,

                PermissionCodes.DocumentView,
                PermissionCodes.DocumentCreate,
                PermissionCodes.DocumentEdit,

                // BÊN PHÁT HÀNH ĐƯỢC ĐẨY/SỬA THÔNG TIN, KHÔNG DUYỆT/XÓA
                PermissionCodes.DocumentSubmit,

                PermissionCodes.TaskView,
                PermissionCodes.ReportView
            };
        }

        // ================= STAFF =================
        if (string.Equals(roleName, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PermissionCodes.DashboardView,

                PermissionCodes.DocumentView,
                PermissionCodes.DocumentCreate,
                PermissionCodes.DocumentEdit,

                // CHỈ GỬI DUYỆT
                PermissionCodes.DocumentSubmit,

                PermissionCodes.TaskView
            };
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
