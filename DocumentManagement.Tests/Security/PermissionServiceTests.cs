using DocumentManagement.Application.Models;
using DocumentManagement.Application.Security;
using Xunit;

namespace DocumentManagement.Tests.Security;

public sealed class PermissionServiceTests
{
    private readonly PermissionService _permissionService = new();

    [Fact]
    public void HasPermission_ShouldReturnFalse_WhenUserIsMissing()
    {
        Assert.False(_permissionService.HasPermission(null, PermissionCodes.DocumentView));
    }

    [Fact]
    public void HasPermission_ShouldReturnFalse_WhenPermissionCodeIsMissing()
    {
        var user = CreateUser("Admin");

        Assert.False(_permissionService.HasPermission(user, ""));
        Assert.False(_permissionService.HasPermission(user, "   "));
    }

    [Fact]
    public void Admin_ShouldHaveFullDocumentAndBackupPermissions()
    {
        var user = CreateUser("Admin");

        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DashboardView));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentCreate));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentEdit));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentDelete));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentApprove));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.BackupCreate));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.BackupRestore));
    }

    [Fact]
    public void Manager_ShouldEditAndApprove_ButNotCreateDeleteOrBackup()
    {
        var user = CreateUser("Manager");

        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DashboardView));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentView));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentEdit));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentApprove));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentReject));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.DocumentCreate));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.DocumentDelete));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.BackupCreate));
    }

    [Fact]
    public void Staff_ShouldCreateEditSubmit_ButNotApproveDeleteOrBackup()
    {
        var user = CreateUser("Staff");

        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DashboardView));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentView));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentCreate));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentEdit));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentSubmit));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.DocumentApprove));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.DocumentDelete));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.BackupRestore));
    }

    [Fact]
    public void Publisher_ShouldCreateEditSubmitAndExport_ButNotApproveDeleteOrBackup()
    {
        var user = CreateUser("Publisher");

        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DashboardView));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentView));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentCreate));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentEdit));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.DocumentSubmit));
        Assert.True(_permissionService.HasPermission(user, PermissionCodes.ReportView));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.DocumentApprove));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.DocumentDelete));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.BackupRestore));
    }

    [Fact]
    public void UnknownRole_ShouldHaveNoPermissions()
    {
        var user = CreateUser("Guest");

        Assert.False(_permissionService.HasPermission(user, PermissionCodes.DashboardView));
        Assert.False(_permissionService.HasPermission(user, PermissionCodes.DocumentView));
    }

    [Fact]
    public void RoleAndPermissionMatching_ShouldBeCaseInsensitive()
    {
        var user = CreateUser("admin");

        Assert.True(_permissionService.HasPermission(user, "DOCUMENT.DELETE"));
    }

    private static UserSession CreateUser(string role)
    {
        return new UserSession
        {
            Id = 1,
            Username = "test-user",
            DisplayName = "Test User",
            Roles = new List<string> { role }
        };
    }
}
