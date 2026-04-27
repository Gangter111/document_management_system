namespace DocumentManagement.Contracts.Common;

public class LookupItemDto
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}