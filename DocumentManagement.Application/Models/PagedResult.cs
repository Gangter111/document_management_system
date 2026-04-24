using System.Collections.Generic;

namespace DocumentManagement.Application.Models
{
    /// <summary>
    /// Kết quả phân trang dùng chung cho các màn hình danh sách lớn.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu phần tử.</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// Danh sách item của trang hiện tại.
        /// </summary>
        public IReadOnlyList<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Tổng số bản ghi thỏa điều kiện lọc.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Trang hiện tại, bắt đầu từ 1.
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Số bản ghi trên mỗi trang.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Tổng số trang.
        /// </summary>
        public int TotalPages
        {
            get
            {
                if (PageSize <= 0)
                    return 0;

                return (TotalCount + PageSize - 1) / PageSize;
            }
        }
    }
}