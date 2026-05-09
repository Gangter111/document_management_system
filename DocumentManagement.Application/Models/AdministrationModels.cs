namespace DocumentManagement.Application.Models;

public class CatalogItemModel
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class SaveCatalogItemModel
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class UserAdminModel
{
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public long RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class RoleModel
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class SaveUserModel
{
    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public long RoleId { get; set; }

    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;
}
