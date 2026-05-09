namespace DocumentManagement.Contracts.System;

public class UserAdminDto
{
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public long RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class RoleDto
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class SaveUserRequest
{
    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public long RoleId { get; set; }

    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;
}
