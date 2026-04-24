using System;
using DocumentManagement.Domain.Enums;

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

    // Tạm thời vẫn giữ StatusId để không làm vỡ Repository/ViewModel hiện tại.
    public long? StatusId { get; set; }

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

    public DocumentStatus Status => StatusId switch
    {
        1 => DocumentStatus.Draft,
        2 => DocumentStatus.PendingApproval,
        3 => DocumentStatus.Approved,
        4 => DocumentStatus.Issued,
        5 => DocumentStatus.Archived,
        6 => DocumentStatus.Rejected,
        _ => DocumentStatus.Draft
    };

    public string StatusCode => Status switch
    {
        DocumentStatus.Draft => "DRAFT",
        DocumentStatus.PendingApproval => "PENDING_APPROVAL",
        DocumentStatus.Approved => "APPROVED",
        DocumentStatus.Issued => "ISSUED",
        DocumentStatus.Archived => "ARCHIVED",
        DocumentStatus.Rejected => "REJECTED",
        _ => "DRAFT"
    };

    public string StatusText => Status switch
    {
        DocumentStatus.Draft => "Bản nháp",
        DocumentStatus.PendingApproval => "Đang duyệt",
        DocumentStatus.Approved => "Đã duyệt",
        DocumentStatus.Issued => "Đã ban hành",
        DocumentStatus.Archived => "Đã lưu trữ",
        DocumentStatus.Rejected => "Bị từ chối",
        _ => "Chưa xác định"
    };

    public string StatusColor => Status switch
    {
        DocumentStatus.Draft => "#94A3B8",
        DocumentStatus.PendingApproval => "#F59E0B",
        DocumentStatus.Approved => "#3B82F6",
        DocumentStatus.Issued => "#10B981",
        DocumentStatus.Archived => "#8B5CF6",
        DocumentStatus.Rejected => "#EF4444",
        _ => "#9CA3AF"
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

    public string DocumentDate => !string.IsNullOrEmpty(ReceivedDate)
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

    public void SubmitForApproval(string user)
    {
        if (Status != DocumentStatus.Draft)
            throw new InvalidOperationException("Chỉ văn bản nháp mới được gửi duyệt.");

        StatusId = (long)DocumentStatus.PendingApproval;
        SetUpdated(DateTime.UtcNow, user);
    }

    public void Approve(string user)
    {
        if (Status != DocumentStatus.PendingApproval)
            throw new InvalidOperationException("Chỉ văn bản đang duyệt mới được phê duyệt.");

        StatusId = (long)DocumentStatus.Approved;
        SetUpdated(DateTime.UtcNow, user);
    }

    public void Reject(string user, string? reason)
    {
        if (Status != DocumentStatus.PendingApproval)
            throw new InvalidOperationException("Chỉ văn bản đang duyệt mới được từ chối.");

        StatusId = (long)DocumentStatus.Rejected;
        Notes = reason;
        SetUpdated(DateTime.UtcNow, user);
    }
}