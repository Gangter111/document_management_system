using System.Security.Claims;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.Common;
using DocumentManagement.Contracts.Documents;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfExtractionException = DocumentManagement.Application.Models.PdfExtractionException;
using PdfExtractionJobRequest = DocumentManagement.Application.Models.PdfExtractionJobRequest;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private const long MaxPdfExtractionBytes = 20L * 1024L * 1024L;

    private readonly IDocumentService _documentService;
    private readonly IOcrService? _ocrService;
    private readonly IPdfExtractionWorker? _pdfExtractionWorker;
    private readonly IAuditLogRepository? _auditLogRepository;

    public DocumentsController(
        IDocumentService documentService,
        IOcrService? ocrService = null,
        IPdfExtractionWorker? pdfExtractionWorker = null,
        IAuditLogRepository? auditLogRepository = null)
    {
        _documentService = documentService;
        _ocrService = ocrService;
        _pdfExtractionWorker = pdfExtractionWorker;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> GetAll()
    {
        var documents = await _documentService.GetAllAsync();

        var result = documents
            .Where(document => document.IsActive)
            .Select(ToDto)
            .ToList();

        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResultDto<DocumentDto>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] long? categoryId,
        [FromQuery] long? statusId,
        [FromQuery] string? urgency,
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100)
    {
        var request = new DocumentManagement.Application.Models.DocumentSearchRequest
        {
            Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword,
            CategoryId = categoryId is > 0 ? categoryId : null,
            StatusId = statusId is > 0 ? statusId : null,
            UrgencyLevel = string.IsNullOrWhiteSpace(urgency) ? null : urgency,
            FromDate = string.IsNullOrWhiteSpace(fromDate) ? null : fromDate,
            ToDate = string.IsNullOrWhiteSpace(toDate) ? null : toDate,
            PageNumber = pageNumber <= 0 ? 1 : pageNumber,
            PageSize = pageSize <= 0 ? 100 : pageSize
        };

        var result = await _documentService.SearchPagedAsync(request);

        var dto = new PagedResultDto<DocumentDto>
        {
            Items = result.Items
                .Where(document => document.IsActive)
                .Select(ToDto)
                .ToList(),
            TotalCount = result.TotalCount,
            PageNumber = result.PageNumber,
            PageSize = result.PageSize
        };

        return Ok(dto);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DocumentDto>> GetById(long id)
    {
        var document = await _documentService.GetByIdAsync(id);

        if (document == null || !document.IsActive)
        {
            return NotFound();
        }

        return Ok(ToDto(document));
    }

    [HttpPost]
    public async Task<ActionResult<long>> Create([FromBody] CreateDocumentRequest request)
    {
        var permissionResult = RequireCreatePermission();

        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            return BadRequest("Số văn bản không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Tiêu đề văn bản không được để trống.");
        }

        var document = ToEntity(request);
        document.CreatedBy = GetCurrentUsername();

        var id = await _documentService.CreateAsync(document);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("extract-pdf")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<AutoFillDocumentResultDto>> ExtractPdf(IFormFile file, CancellationToken cancellationToken)
    {
        var permissionResult = RequireCreatePermission();

        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest("Vui lòng chọn file PDF.");
        }

        if (file.Length > MaxPdfExtractionBytes)
        {
            return BadRequest("File PDF vượt quá giới hạn 20 MB.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Hệ thống chỉ hỗ trợ file PDF.");
        }

        if (!await HasPdfHeaderAsync(file, cancellationToken))
        {
            return BadRequest("Nội dung file không phải PDF hợp lệ.");
        }

        var correlationId = HttpContext.TraceIdentifier;
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString("N");

        var tempDirectory = Path.Combine(Path.GetTempPath(), "DocumentManagement", "pdf-extract", SanitizeToken(correlationId));
        Directory.CreateDirectory(tempDirectory);

        var tempPath = Path.Combine(tempDirectory, "upload.pdf");

        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            if (_ocrService == null && _pdfExtractionWorker == null)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Chức năng trích xuất PDF chưa được cấu hình trên máy chủ.");
            }

            using var extractionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            extractionTimeout.CancelAfter(TimeSpan.FromSeconds(30));

            var job = new PdfExtractionJobRequest(
                correlationId,
                Path.GetFileName(tempPath),
                tempPath,
                file.Length,
                TimeSpan.FromSeconds(30));
            var result = _pdfExtractionWorker != null
                ? await _pdfExtractionWorker.ExtractAsync(job, extractionTimeout.Token)
                : await _ocrService!.ExtractAndParseAsync(tempPath, extractionTimeout.Token);

            return Ok(new AutoFillDocumentResultDto
            {
                DocumentNumber = result.DocumentNumber,
                Title = result.Title,
                Summary = result.Summary,
                IssueDate = result.IssueDate,
                SenderName = result.SenderName,
                ReceiverName = result.ReceiverName,
                SignerName = result.SignerName,
                UrgencyLevel = result.UrgencyLevel,
                ContentText = result.ContentText,
                IsFromOcr = result.IsFromOcr,
                OcrConfidence = result.OcrConfidence,
                IsPartialExtraction = result.IsPartialExtraction,
                FailureKind = result.FailureKind,
                FailureMessage = result.FailureMessage,
                DocumentKind = result.DocumentKind,
                RequiresManualReview = result.RequiresManualReview,
                Fields = result.Fields.Select(field => new ExtractedFieldDto
                {
                    FieldName = field.FieldName,
                    Value = field.Value,
                    Confidence = field.Confidence,
                    SourceText = field.SourceText,
                    ExtractionMethod = field.ExtractionMethod,
                    RequiresReview = field.RequiresReview
                }).ToList(),
                ReviewReasons = result.ReviewReasons.ToList(),
                ExtractionTrace = result.ExtractionTrace.Select(trace => new ExtractionTraceDto
                {
                    Stage = trace.Stage,
                    Message = trace.Message
                }).ToList()
            });
        }
        catch (PdfExtractionException ex)
        {
            await WriteAuditAsync("PDF_EXTRACTION_FAILURE", "PdfExtraction", 0, GetCurrentUsername(), ex.FailureKind);
            return BadRequest(new AutoFillDocumentResultDto
            {
                FailureKind = ex.FailureKind,
                FailureMessage = ex.Message
            });
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup for temporary upload files.
            }
        }
    }

    private static string SanitizeToken(string value)
    {
        var filtered = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? Guid.NewGuid().ToString("N") : filtered;
    }

    private static async Task<bool> HasPdfHeaderAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

        return read == 4
               && header[0] == 0x25
               && header[1] == 0x50
               && header[2] == 0x44
               && header[3] == 0x46;
    }

    private async Task WriteAuditAsync(string action, string entityName, long entityId, string username, string? reason)
    {
        if (_auditLogRepository == null)
            return;

        await _auditLogRepository.AddAsync(new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ChangedColumns = "FAILURE",
            NewValues = $"correlationId={HttpContext.TraceIdentifier};reason={reason}",
            Username = username,
            CreatedAt = DateTime.UtcNow
        });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateDocumentRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest("Id trên URL không khớp với Id trong dữ liệu.");
        }

        if (string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            return BadRequest("Số văn bản không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Tiêu đề văn bản không được để trống.");
        }

        var existing = await _documentService.GetByIdAsync(id);

        if (existing == null || !existing.IsActive)
        {
            return NotFound("Không tìm thấy văn bản hoặc văn bản đã bị xóa.");
        }

        var permissionResult = RequireUpdatePermission(existing);

        if (permissionResult != null)
        {
            return permissionResult;
        }

        ApplyUpdate(existing, request);
        existing.UpdatedBy = GetCurrentUsername();

        await _documentService.UpdateAsync(existing);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var permissionResult = RequireDeletePermission();

        if (permissionResult != null)
        {
            return permissionResult;
        }

        var existing = await _documentService.GetByIdAsync(id);

        if (existing == null)
        {
            return NotFound("Không tìm thấy văn bản.");
        }

        if (!existing.IsActive)
        {
            return NoContent();
        }

        await _documentService.SoftDeleteAsync(id);

        return NoContent();
    }

    private ActionResult? RequireCreatePermission()
    {
        var role = GetCurrentRole();

        if (string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized("Thiếu thông tin vai trò người dùng.");
        }

        if (IsAdmin(role) || IsManager(role) || IsPublisher(role) || IsStaff(role))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền tạo văn bản.");
    }

    private ActionResult? RequireUpdatePermission(Document document)
    {
        var role = GetCurrentRole();

        if (string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized("Thiếu thông tin vai trò người dùng.");
        }

        if (IsAdmin(role) || IsManager(role) || IsPublisher(role))
        {
            if (IsAdmin(role))
            {
                return null;
            }

            var userDepartment = GetCurrentDepartment();

            if (string.IsNullOrWhiteSpace(userDepartment))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Người dùng chưa được cấu hình phòng ban.");
            }

            if (string.Equals(
                    document.ProcessingDepartment?.Trim(),
                    userDepartment.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return StatusCode(
                StatusCodes.Status403Forbidden,
                "Bạn chỉ được sửa văn bản thuộc phòng ban của mình.");
        }

        return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền sửa văn bản.");
    }

    private ActionResult? RequireDeletePermission()
    {
        var role = GetCurrentRole();

        if (string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized("Thiếu thông tin vai trò người dùng.");
        }

        if (IsAdmin(role))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền xóa văn bản.");
    }

    private string GetCurrentRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value
               ?? User.FindFirst("role")?.Value
               ?? string.Empty;
    }

    private string GetCurrentUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
               ?? User.Identity?.Name
               ?? "system";
    }

    private string GetCurrentDepartment()
    {
        return User.FindFirst("department")?.Value
               ?? string.Empty;
    }

    private static bool IsAdmin(string role)
    {
        return string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManager(string role)
    {
        return string.Equals(role, "MANAGER", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublisher(string role)
    {
        return string.Equals(role, "PUBLISHER", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Publisher", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStaff(string role)
    {
        return string.Equals(role, "STAFF", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase);
    }

    private static DocumentDto ToDto(Document document)
    {
        return new DocumentDto
        {
            Id = document.Id,
            DocumentType = document.DocumentType,
            DocumentNumber = document.DocumentNumber,
            ReferenceNumber = document.ReferenceNumber,
            Title = document.Title,
            Summary = document.Summary,
            ContentText = document.ContentText,
            IssueDate = document.IssueDate,
            ReceivedDate = document.ReceivedDate,
            DueDate = document.DueDate,
            SenderName = document.SenderName,
            ReceiverName = document.ReceiverName,
            SignerName = document.SignerName,
            CategoryId = document.CategoryId,
            StatusId = document.StatusId,
            ConfidentialityLevel = document.ConfidentialityLevel,
            UrgencyLevel = document.UrgencyLevel,
            ProcessingDepartment = document.ProcessingDepartment,
            AssignedTo = document.AssignedTo,
            Notes = document.Notes,
            IsActive = document.IsActive,
            IsExpired = document.IsExpired,
            OcrStatus = document.OcrStatus,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            CreatedBy = document.CreatedBy,
            UpdatedBy = document.UpdatedBy,
            StatusCode = document.StatusCode,
            StatusText = document.StatusText,
            StatusColor = document.StatusColor,
            UrgencyText = document.UrgencyText,
            UrgencyColor = document.UrgencyColor,
            DocumentDate = document.DocumentDate
        };
    }

    private static Document ToEntity(CreateDocumentRequest request)
    {
        return new Document
        {
            DocumentType = string.IsNullOrWhiteSpace(request.DocumentType)
                ? "INCOMING"
                : request.DocumentType,
            DocumentNumber = request.DocumentNumber,
            ReferenceNumber = request.ReferenceNumber,
            Title = request.Title,
            Summary = request.Summary,
            ContentText = request.ContentText,
            IssueDate = request.IssueDate,
            ReceivedDate = request.ReceivedDate,
            DueDate = request.DueDate,
            SenderName = request.SenderName,
            ReceiverName = request.ReceiverName,
            SignerName = request.SignerName,
            CategoryId = request.CategoryId,
            StatusId = request.StatusId is > 0 ? request.StatusId : 4,
            ConfidentialityLevel = string.IsNullOrWhiteSpace(request.ConfidentialityLevel)
                ? "NORMAL"
                : request.ConfidentialityLevel,
            UrgencyLevel = string.IsNullOrWhiteSpace(request.UrgencyLevel)
                ? "NORMAL"
                : request.UrgencyLevel,
            ProcessingDepartment = request.ProcessingDepartment,
            AssignedTo = request.AssignedTo,
            Notes = request.Notes,
            IsActive = true,
            IsExpired = false,
            OcrStatus = string.IsNullOrWhiteSpace(request.OcrStatus)
                ? "PENDING"
                : request.OcrStatus,
            CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy)
                ? "system"
                : request.CreatedBy
        };
    }

    private static void ApplyUpdate(Document document, UpdateDocumentRequest request)
    {
        document.DocumentType = string.IsNullOrWhiteSpace(request.DocumentType)
            ? "INCOMING"
            : request.DocumentType;
        document.DocumentNumber = request.DocumentNumber;
        document.ReferenceNumber = request.ReferenceNumber;
        document.Title = request.Title;
        document.Summary = request.Summary;
        document.ContentText = request.ContentText;
        document.IssueDate = request.IssueDate;
        document.ReceivedDate = request.ReceivedDate;
        document.DueDate = request.DueDate;
        document.SenderName = request.SenderName;
        document.ReceiverName = request.ReceiverName;
        document.SignerName = request.SignerName;
        document.CategoryId = request.CategoryId;
        document.StatusId = request.StatusId is > 0 ? request.StatusId : 4;
        document.ConfidentialityLevel = string.IsNullOrWhiteSpace(request.ConfidentialityLevel)
            ? "NORMAL"
            : request.ConfidentialityLevel;
        document.UrgencyLevel = string.IsNullOrWhiteSpace(request.UrgencyLevel)
            ? "NORMAL"
            : request.UrgencyLevel;
        document.ProcessingDepartment = request.ProcessingDepartment;
        document.AssignedTo = request.AssignedTo;
        document.Notes = request.Notes;
        document.IsExpired = request.IsExpired;
        document.OcrStatus = string.IsNullOrWhiteSpace(request.OcrStatus)
            ? "PENDING"
            : request.OcrStatus;
        document.UpdatedBy = string.IsNullOrWhiteSpace(request.UpdatedBy)
            ? "system"
            : request.UpdatedBy;
    }
}
