namespace DocumentManagement.Contracts.Common;

public class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();

    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }

    public int TotalPages
    {
        get
        {
            if (PageSize <= 0)
            {
                return 0;
            }

            return (TotalCount + PageSize - 1) / PageSize;
        }
    }
}
