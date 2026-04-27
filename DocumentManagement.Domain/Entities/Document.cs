using System;

namespace DocumentManagement.Domain.Entities;

public class Document
{
    public long Id { get; set; }

    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ContentText { get; set; }

    public string? IssueDate { get; set; }
    public string? ReceivedDate { get; set; }
    public string? DueDate { get; set; }

    public string? SenderName { get; set; }
    public string? ReceiverName { get; set; }
    public string? SignerName { get; set; }

    public long? CategoryId { get; set; }

    // Giữ StatusId để không phá DB/repository hiện tại.
    // Từ giờ chỉ dùng tối giản:
    // 1 = Nháp
    // 4 = Đã ban hành
    // 5 = Đã lưu trữ
    public long? StatusId { get; set; } = 4;

    public string ConfidentialityLevel { get; set; } = "NORMAL";
    public string UrgencyLevel { get; set; } = "NORMAL";

    public string? ProcessingDepartment { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsExpired { get; set; } = false;

    public string OcrStatus { get; set; } = "PENDING";

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public string StatusCode => StatusId switch
    {
        1 => "DRAFT",
        4 => "ISSUED",
        5 => "ARCHIVED",
        _ => "ISSUED"
    };

    public string StatusText => StatusId switch
    {
        1 => "Bản nháp",
        4 => "Đã ban hành",
        5 => "Đã lưu trữ",
        _ => "Đã ban hành"
    };

    public string StatusColor => StatusId switch
    {
        1 => "#94A3B8",
        4 => "#10B981",
        5 => "#8B5CF6",
        _ => "#10B981"
    };

    public string UrgencyText => UrgencyLevel switch
    {
        "URGENT" => "KHẨN",
        "VERY_URGENT" => "HỎA TỐC",
        _ => "THƯỜNG"
    };

    public string UrgencyColor => UrgencyLevel switch
    {
        "URGENT" => "#F59E0B",
        "VERY_URGENT" => "#EF4444",
        _ => "#10B981"
    };

    public string DocumentDate => !string.IsNullOrWhiteSpace(ReceivedDate)
        ? ReceivedDate
        : IssueDate ?? string.Empty;

    public void SetCreated(DateTime now, string user)
    {
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = user;
        UpdatedBy = user;
    }

    public void SetUpdated(DateTime now, string user)
    {
        UpdatedAt = now;
        UpdatedBy = user;
    }

    public void Deactivate(string user)
    {
        IsActive = false;
        SetUpdated(DateTime.UtcNow, user);
    }

    public void MarkAsIssued(string user)
    {
        StatusId = 4;
        SetUpdated(DateTime.UtcNow, user);
    }

    public void Archive(string user)
    {
        StatusId = 5;
        SetUpdated(DateTime.UtcNow, user);
    }
}