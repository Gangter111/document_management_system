using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Data;
using System.Globalization;

namespace DocumentManagement.Infrastructure.Services;

public class DemoDataService : IDemoDataService
{
    public const string DemoCreatedBy = "demo-seed";
    public const string DemoBatchId = "DEMO-QA-2026-05";
    private const int RequiredDemoCount = 100;
    private const int IssuedDemoCount = 68;
    private const int EffectiveIssuedCount = 45;
    private const int ExpiredIssuedCount = IssuedDemoCount - EffectiveIssuedCount;
    private const int ArchivedDemoCount = 24;
    private const int ArchivedDocumentsPerMonth = ArchivedDemoCount / 12;
    private const int DraftDemoCount = 8;

    private static readonly DateTime DemoAnchorDate = new(2026, 5, 14);
    private static readonly int[] MonthlyIssuedCounts = { 3, 4, 5, 4, 6, 7, 5, 8, 7, 9, 6, 4 };
    private static readonly int[] DraftMonthOffsets = { 8, 9, 9, 10, 10, 11, 11, 11 };

    private static readonly string[] Departments =
    {
        "Phòng Kinh doanh",
        "Phòng Kế toán",
        "Phòng Pháp chế",
        "Phòng HCNS",
        "Phòng CNTT",
        "Phòng Vận hành",
        "Ban Giám đốc",
        "Phòng Kinh doanh",
        "Phòng Kế toán",
        "Phòng Vận hành",
        "Phòng Kinh doanh",
        "Phòng Pháp chế",
        "Phòng Kế toán",
        "Phòng HCNS"
    };

    private static readonly string[] ExternalOrganizations =
    {
        "UBND Thành phố",
        "Sở Nội vụ",
        "Sở Tài chính",
        "Sở Thông tin và Truyền thông",
        "Cục Thuế Thành phố",
        "Bảo hiểm Xã hội Thành phố",
        "Ban Quản lý dự án",
        "Ngân hàng TMCP Phương Nam",
        "Công ty TNHH Minh An",
        "Trung tâm Lưu trữ"
    };

    private static readonly string[] Signers =
    {
        "Nguyễn Văn An",
        "Trần Thị Bình",
        "Lê Minh Quang",
        "Phạm Thu Hà",
        "Đỗ Hoàng Nam",
        "Võ Minh Đức",
        "Hoàng Thanh Tâm",
        "Bùi Quốc Huy",
        "Mai Anh Thư",
        "Đặng Hữu Phúc"
    };

    private static readonly string[] Handlers = { "manager", "publisher", "staff", "admin" };

    private static readonly DemoDocumentKind[] DocumentKinds =
    {
        new("Quyết định", "QD", 2, "OUTGOING"),
        new("Thông báo", "TB", 3, "OUTGOING"),
        new("Công văn", "CV", 1, "INCOMING"),
        new("Báo cáo", "BC", 4, "OUTGOING"),
        new("Kế hoạch", "KH", 4, "OUTGOING"),
        new("Tờ trình", "TTr", 1, "OUTGOING"),
        new("Biên bản", "BB", 4, "OUTGOING"),
        new("Hợp đồng", "HD", 1, "INCOMING"),
        new("Quy chế", "QC", 2, "OUTGOING"),
        new("Quy định", "QDi", 2, "OUTGOING")
    };

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

        foreach (var seed in BuildDocuments())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = await _documentService.CreateAsync(seed.Document);
            await UpdateDemoTimestampsAsync(id, seed.CreatedAt, seed.UpdatedAt, cancellationToken);
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

    private async Task UpdateDemoTimestampsAsync(
        long id,
        DateTime createdAt,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE documents
SET created_at = @createdAt,
    updated_at = @updatedAt
WHERE id = @id;";
        command.AddParameter("createdAt", FormatTimestamp(createdAt));
        command.AddParameter("updatedAt", FormatTimestamp(updatedAt));
        command.AddParameter("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<DemoDocumentSeed> BuildDocuments()
    {
        var specs = BuildDocumentSpecs();

        return specs
            .OrderBy(spec => spec.IssueDate)
            .ThenBy(spec => spec.SortOrder)
            .Select((spec, index) => BuildSeed(index + 1, spec));
    }

    private static List<DemoDocumentSpec> BuildDocumentSpecs()
    {
        var specs = new List<DemoDocumentSpec>(RequiredDemoCount);
        var startMonth = new DateTime(DemoAnchorDate.Year, DemoAnchorDate.Month, 1).AddMonths(-11);
        var issuedOrdinal = 0;

        for (var monthOffset = 0; monthOffset < MonthlyIssuedCounts.Length; monthOffset++)
        {
            var month = startMonth.AddMonths(monthOffset);
            var documentsInMonth = MonthlyIssuedCounts[monthOffset];

            for (var indexInMonth = 0; indexInMonth < documentsInMonth; indexInMonth++)
            {
                issuedOrdinal++;
                specs.Add(new DemoDocumentSpec(
                    IssueDate: GetIssueDate(month, indexInMonth, issuedOrdinal),
                    StatusId: 4,
                    IsExpired: issuedOrdinal <= ExpiredIssuedCount,
                    KindIndex: issuedOrdinal + monthOffset,
                    DepartmentIndex: issuedOrdinal + monthOffset * 2,
                    SignerIndex: issuedOrdinal + monthOffset,
                    OrganizationIndex: issuedOrdinal + monthOffset * 3,
                    SortOrder: specs.Count));
            }
        }

        var archivedOrdinal = 0;
        for (var monthOffset = 0; monthOffset < 12; monthOffset++)
        {
            var month = startMonth.AddMonths(monthOffset);
            for (var indexInMonth = 0; indexInMonth < ArchivedDocumentsPerMonth; indexInMonth++)
            {
                archivedOrdinal++;
                specs.Add(new DemoDocumentSpec(
                    IssueDate: GetIssueDate(month, indexInMonth + 2, archivedOrdinal + 70),
                    StatusId: 5,
                    IsExpired: false,
                    KindIndex: archivedOrdinal + 4,
                    DepartmentIndex: archivedOrdinal * 2,
                    SignerIndex: archivedOrdinal + 3,
                    OrganizationIndex: archivedOrdinal + 5,
                    SortOrder: specs.Count));
            }
        }

        for (var draftOrdinal = 0; draftOrdinal < DraftDemoCount; draftOrdinal++)
        {
            var month = startMonth.AddMonths(DraftMonthOffsets[draftOrdinal]);
            specs.Add(new DemoDocumentSpec(
                IssueDate: GetIssueDate(month, draftOrdinal, draftOrdinal + 130),
                StatusId: 1,
                IsExpired: false,
                KindIndex: draftOrdinal + 6,
                DepartmentIndex: draftOrdinal * 3,
                SignerIndex: draftOrdinal + 6,
                OrganizationIndex: draftOrdinal + 2,
                SortOrder: specs.Count));
        }

        if (issuedOrdinal != IssuedDemoCount || specs.Count != RequiredDemoCount)
        {
            throw new InvalidOperationException("Demo data distribution is misconfigured.");
        }

        return specs;
    }

    private static DemoDocumentSeed BuildSeed(int n, DemoDocumentSpec spec)
    {
        var kind = DocumentKinds[spec.KindIndex % DocumentKinds.Length];
        var department = Departments[spec.DepartmentIndex % Departments.Length];
        var organization = ExternalOrganizations[spec.OrganizationIndex % ExternalOrganizations.Length];
        var signer = Signers[spec.SignerIndex % Signers.Length];
        var documentNumber = $"DEMO-2026-{n:000}";
        var referenceNumber = $"{kind.CodePrefix}-{spec.IssueDate:yyyy}/{spec.IssueDate:MM}-{n:000}";
        var createdAt = spec.IssueDate.AddDays(-((n % 5) + 1)).AddHours(8 + (n % 8)).AddMinutes((n * 7) % 50);
        var updatedAt = GetUpdatedAt(spec, createdAt, n);

        var document = new Document
        {
            DocumentType = kind.Direction,
            DocumentNumber = documentNumber,
            ReferenceNumber = referenceNumber,
            Title = GetTitle(kind.Name, spec.IssueDate, department, n),
            Summary = GetSummary(kind.Name, department, spec.StatusId, spec.IsExpired),
            ContentText = GetContentText(kind.Name, department, referenceNumber),
            IssueDate = FormatDate(spec.IssueDate),
            ReceivedDate = FormatDate(spec.IssueDate.AddDays(kind.Direction == "INCOMING" ? 1 : 0)),
            DueDate = GetDueDate(spec, n),
            SenderName = kind.Direction == "INCOMING" ? organization : department,
            ReceiverName = kind.Direction == "INCOMING" ? department : organization,
            SignerName = signer,
            CategoryId = kind.CategoryId,
            StatusId = spec.StatusId,
            ConfidentialityLevel = n % 17 == 0 ? "CONFIDENTIAL" : "NORMAL",
            UrgencyLevel = GetUrgency(n, spec.StatusId),
            ProcessingDepartment = department,
            AssignedTo = Handlers[n % Handlers.Length],
            Notes = $"{DemoBatchId}; {kind.Name}; {GetOperationalNote(spec.StatusId, spec.IsExpired)}",
            IsExpired = spec.IsExpired,
            OcrStatus = "DEMO",
            CreatedBy = DemoCreatedBy,
            UpdatedBy = DemoCreatedBy
        };

        return new DemoDocumentSeed(document, createdAt, updatedAt);
    }

    private static DateTime GetIssueDate(DateTime month, int indexInMonth, int ordinal)
    {
        var maxDay = month.Year == DemoAnchorDate.Year && month.Month == DemoAnchorDate.Month
            ? Math.Max(2, DemoAnchorDate.Day - 1)
            : Math.Min(26, DateTime.DaysInMonth(month.Year, month.Month));
        var day = 2 + ((indexInMonth * 5 + ordinal * 3) % Math.Max(1, maxDay - 1));

        return new DateTime(month.Year, month.Month, Math.Min(day, maxDay));
    }

    private static DateTime GetUpdatedAt(DemoDocumentSpec spec, DateTime createdAt, int n)
    {
        var updatedAt = spec.StatusId switch
        {
            1 => createdAt.AddDays((n % 4) + 1),
            5 => spec.IssueDate.AddDays(80 + (n % 20)).AddHours(15),
            _ => spec.IssueDate.AddDays((n % 9) + 1).AddHours(16)
        };

        return updatedAt > DemoAnchorDate
            ? DemoAnchorDate.AddHours(-(n % 8)).AddMinutes(-((n * 3) % 45))
            : updatedAt;
    }

    private static string? GetDueDate(DemoDocumentSpec spec, int n)
    {
        if (spec.StatusId == 1)
        {
            return null;
        }

        var dueDate = spec.IsExpired
            ? spec.IssueDate.AddDays(72 + (n % 22))
            : DemoAnchorDate.AddDays(95 + (n % 120));

        return FormatDate(dueDate);
    }

    private static string GetUrgency(int n, long statusId)
    {
        if (statusId == 1)
        {
            return "NORMAL";
        }

        if (n % 19 == 0 || n % 23 == 0)
        {
            return "VERY_URGENT";
        }

        return n % 7 == 0 || n % 11 == 0
            ? "URGENT"
            : "NORMAL";
    }

    private static string GetTitle(string kind, DateTime issueDate, string department, int n)
    {
        var month = issueDate.ToString("MM", CultureInfo.InvariantCulture);
        var quarter = ((issueDate.Month - 1) / 3) + 1;

        return kind switch
        {
            "Quyết định" => n % 2 == 0
                ? $"Quyết định phân công nhiệm vụ {department} quý {quarter}"
                : $"Quyết định phê duyệt kế hoạch vận hành tháng {month}",
            "Thông báo" => n % 2 == 0
                ? $"Thông báo lịch họp điều hành tuần {((n - 1) % 52) + 1}"
                : $"Thông báo triển khai quy trình xử lý văn bản tháng {month}",
            "Công văn" => n % 2 == 0
                ? $"Công văn phối hợp rà soát hồ sơ lưu trữ tháng {month}"
                : $"Công văn triển khai hệ thống lưu trữ văn bản điện tử",
            "Báo cáo" => n % 2 == 0
                ? $"Báo cáo tình hình vận hành tháng {month}"
                : $"Báo cáo tiến độ xử lý văn bản nội bộ quý {quarter}",
            "Kế hoạch" => n % 2 == 0
                ? $"Kế hoạch đào tạo nhân sự nội bộ quý {quarter}"
                : $"Kế hoạch kiểm tra hồ sơ nghiệp vụ tháng {month}",
            "Tờ trình" => n % 2 == 0
                ? $"Tờ trình phê duyệt ngân sách mua sắm quý {quarter}"
                : $"Tờ trình điều chỉnh kế hoạch vận hành tháng {month}",
            "Biên bản" => n % 2 == 0
                ? $"Biên bản nghiệm thu thiết bị CNTT đợt {((n - 1) % 6) + 1}"
                : $"Biên bản họp rà soát hồ sơ {department}",
            "Hợp đồng" => n % 2 == 0
                ? $"Hợp đồng dịch vụ bảo trì hệ thống lưu trữ năm 2026"
                : $"Hợp đồng cung cấp thiết bị văn phòng quý {quarter}",
            "Quy chế" => n % 2 == 0
                ? $"Quy chế quản lý hồ sơ điện tử nội bộ"
                : $"Quy chế phối hợp xử lý văn bản liên phòng ban",
            _ => n % 2 == 0
                ? $"Quy định luân chuyển văn bản nội bộ tháng {month}"
                : $"Quy định cập nhật phân quyền khai thác hồ sơ"
        };
    }

    private static string GetSummary(string kind, string department, long statusId, bool isExpired)
    {
        var state = statusId switch
        {
            1 => "đang ở trạng thái dự thảo nội bộ",
            5 => "đã hoàn tất và chuyển lưu trữ",
            _ when isExpired => "đã ban hành và hết hiệu lực theo thời hạn xử lý",
            _ => "đã ban hành và còn hiệu lực theo kế hoạch"
        };

        return $"{kind} thuộc {department}, {state}; dùng làm dữ liệu demo cho tìm kiếm, dashboard, báo cáo và lưu trữ.";
    }

    private static string GetContentText(string kind, string department, string referenceNumber)
    {
        return $"{kind} số tham chiếu {referenceNumber}. Nội dung demo mô phỏng nghiệp vụ {department}, được tạo qua application service và lưu trong database thật để toàn bộ màn hình dùng cùng một nguồn dữ liệu.";
    }

    private static string GetOperationalNote(long statusId, bool isExpired)
    {
        return statusId switch
        {
            1 => "Dự thảo nội bộ dùng để kiểm thử trạng thái chưa ban hành.",
            5 => "Đã lưu trữ để kiểm thử Archive và Restore.",
            _ when isExpired => "Văn bản đã hết hiệu lực để kiểm thử thống kê và cảnh báo.",
            _ => "Dữ liệu demo vận hành còn hiệu lực."
        };
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatTimestamp(DateTime date)
    {
        return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private sealed record DemoDocumentKind(string Name, string CodePrefix, long CategoryId, string Direction);

    private sealed record DemoDocumentSpec(
        DateTime IssueDate,
        long StatusId,
        bool IsExpired,
        int KindIndex,
        int DepartmentIndex,
        int SignerIndex,
        int OrganizationIndex,
        int SortOrder);

    private sealed record DemoDocumentSeed(Document Document, DateTime CreatedAt, DateTime UpdatedAt);
}
