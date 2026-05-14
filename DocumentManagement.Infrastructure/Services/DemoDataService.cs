using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Data;

namespace DocumentManagement.Infrastructure.Services;

public class DemoDataService : IDemoDataService
{
    public const string DemoCreatedBy = "demo-seed";
    public const string DemoBatchId = "DEMO-QA-2026-05";
    private const int RequiredDemoCount = 40;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDocumentService _documentService;

    public DemoDataService(
        IDbConnectionFactory connectionFactory,
        IDocumentService documentService)
    {
        _connectionFactory = connectionFactory;
        _documentService = documentService;
    }

    public async Task<int> EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var activeDemoCount = await GetActiveDemoCountAsync(cancellationToken);
        if (activeDemoCount == RequiredDemoCount)
        {
            return 0;
        }

        if (activeDemoCount > 0)
        {
            await ClearAsync(cancellationToken);
        }

        await ClearLegacySeedRowsAsync(cancellationToken);

        foreach (var document in BuildDocuments())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _documentService.CreateAsync(document);
        }

        return RequiredDemoCount;
    }

    public async Task<int> ClearAsync(CancellationToken cancellationToken = default)
    {
        var ids = await GetActiveDemoIdsAsync(includeLegacySeed: false, cancellationToken);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _documentService.SoftDeleteAsync(id);
        }

        return ids.Count;
    }

    public async Task<int> GetActiveDemoCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(*)
FROM documents
WHERE is_active = 1
  AND document_number LIKE 'DEMO-2026-%'
  AND notes LIKE @batchMarker;";
        command.AddParameter("batchMarker", "%" + DemoBatchId + "%");

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result ?? 0);
    }

    private async Task ClearLegacySeedRowsAsync(CancellationToken cancellationToken)
    {
        var legacyIds = await GetActiveDemoIdsAsync(includeLegacySeed: true, cancellationToken);
        foreach (var id in legacyIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _documentService.SoftDeleteAsync(id);
        }
    }

    private async Task<List<long>> GetActiveDemoIdsAsync(bool includeLegacySeed, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = includeLegacySeed
            ? @"
SELECT id
FROM documents
WHERE is_active = 1
  AND (
        (document_number LIKE 'DEMO-2026-%' AND notes LIKE @batchMarker)
        OR (created_by = 'seed' AND document_number LIKE 'QA-2026-%' AND ocr_status = 'SEEDED')
      )
ORDER BY id;"
            : @"
SELECT id
FROM documents
WHERE is_active = 1
  AND document_number LIKE 'DEMO-2026-%'
  AND notes LIKE @batchMarker
ORDER BY id;";
        command.AddParameter("batchMarker", "%" + DemoBatchId + "%");

        var ids = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(Convert.ToInt64(reader["id"]));
        }

        return ids;
    }

    private static IEnumerable<Document> BuildDocuments()
    {
        var senders = new[]
        {
            "UBND Thành phố",
            "Sở Nội vụ",
            "Sở Tài chính",
            "Ban Quản lý dự án",
            "Công ty TNHH Minh An",
            "Trung tâm Lưu trữ"
        };

        var departments = new[]
        {
            "Ban Giám đốc",
            "Phòng HCNS",
            "Phòng Kinh doanh",
            "Phòng Kế toán",
            "Phòng Pháp chế"
        };

        var handlers = new[] { "manager", "publisher", "staff", "admin" };
        var signers = new[] { "Nguyễn Văn An", "Trần Thị Bình", "Lê Minh Quang", "Phạm Thu Hà", "Đỗ Hoàng Nam" };

        for (var n = 1; n <= RequiredDemoCount; n++)
        {
            var statusId = GetStatusId(n);
            var dueDate = IsExpired(n)
                ? new DateTime(2026, 2, 1).AddDays(-n)
                : new DateTime(2026, 2, 1).AddDays(n * 2);
            var issueDate = new DateTime(2026, 1, 5).AddDays(n * 3);

            yield return new Document
            {
                DocumentType = n % 3 == 0 ? "OUTGOING" : "INCOMING",
                DocumentNumber = $"DEMO-2026-{n:000}",
                ReferenceNumber = $"PG-2026/{n:000}",
                Title = GetTitle(n),
                Summary = "Dữ liệu demo QA cục bộ dùng cho kiểm thử lọc, tìm kiếm, phân trang, báo cáo và lưu trữ.",
                ContentText = "Nội dung demo được tạo qua application service và lưu trong database thật để các màn hình dùng cùng một nguồn dữ liệu.",
                IssueDate = issueDate.ToString("yyyy-MM-dd"),
                ReceivedDate = issueDate.AddDays(1).ToString("yyyy-MM-dd"),
                DueDate = dueDate.ToString("yyyy-MM-dd"),
                SenderName = senders[n % senders.Length],
                ReceiverName = departments[n % departments.Length],
                SignerName = signers[n % signers.Length],
                CategoryId = (n % 4) + 1,
                StatusId = statusId,
                ConfidentialityLevel = n % 18 == 0 ? "CONFIDENTIAL" : "NORMAL",
                UrgencyLevel = GetUrgency(n),
                ProcessingDepartment = departments[n % departments.Length],
                AssignedTo = handlers[n % handlers.Length],
                Notes = $"{DemoBatchId}; {(statusId == 5 ? "Đã lưu trữ để kiểm thử Archive và Restore." : "Dữ liệu demo vận hành.")}",
                IsExpired = IsExpired(n),
                OcrStatus = "DEMO",
                CreatedBy = DemoCreatedBy,
                UpdatedBy = DemoCreatedBy
            };
        }
    }

    private static long GetStatusId(int n)
    {
        if (n is 5 or 10 or 15 or 20 or 25 or 30 or 35 or 40)
        {
            return 5;
        }

        if (n is 1 or 7 or 13 or 19 or 31)
        {
            return 1;
        }

        return 4;
    }

    private static bool IsExpired(int n)
    {
        return n is 6 or 12 or 18 or 24 or 32 or 38;
    }

    private static string GetUrgency(int n)
    {
        if (n is 4 or 12 or 22 or 34)
        {
            return "VERY_URGENT";
        }

        return n is 2 or 9 or 16 or 23 or 28 or 37
            ? "URGENT"
            : "NORMAL";
    }

    private static string GetTitle(int n)
    {
        return (n % 8) switch
        {
            0 => $"Rà soát hồ sơ hợp đồng mua sắm quý {(n % 4) + 1}",
            1 => $"Thông báo lịch họp điều hành tuần {n}",
            2 => $"Báo cáo tiến độ xử lý văn bản nội bộ số {n}",
            3 => $"Quyết định phân công xử lý hồ sơ dự án {n}",
            4 => $"Công văn phối hợp kiểm tra hiện trường đợt {n}",
            5 => $"Kế hoạch đào tạo nghiệp vụ văn thư tháng {(n % 12) + 1}",
            6 => $"Tờ trình phê duyệt ngân sách vận hành số {n}",
            _ => $"Biên bản nghiệm thu hạng mục hành chính số {n}"
        };
    }
}
