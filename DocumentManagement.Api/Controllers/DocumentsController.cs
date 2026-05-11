using DocumentManagement.Api.Security;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.Common;
using DocumentManagement.Contracts.Documents;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private const int DefaultPageSize = 100;
    private const int MaxPageSize = 200;

    private readonly IDocumentService _documentService;
    private readonly IOcrService? _ocrService;

    public DocumentsController(
        IDocumentService documentService,
        IOcrService? ocrService = null)
    {
        _documentService = documentService;
        _ocrService = ocrService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> GetAll()
    {
        var documents = await _documentService.GetAllAsync();

        var result = documents
            .Where(document => document.IsActive)
            .Where(CanReadDocument)
            .Take(MaxPageSize)
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
            PageSize = NormalizePageSize(pageSize)
        };
        ApplyReadScope(request);

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

        if (!CanReadDocument(document))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                "Bạn không có quyền xem văn bản này.");
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
        document.CreatedBy = User.GetUsername();

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

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Hệ thống chỉ hỗ trợ file PDF.");
        }

        if (!IsAllowedPdfContentType(file.ContentType))
        {
            return BadRequest("Content-Type của file PDF không hợp lệ.");
        }

        if (!await HasPdfSignatureAsync(file, cancellationToken))
        {
            return BadRequest("Nội dung file không phải PDF hợp lệ.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "DocumentManagement", "pdf-extract");
        Directory.CreateDirectory(tempDirectory);

        var tempPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.pdf");

        try
        {
            await using (var stream = System.IO.File.Create(tempPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            if (_ocrService == null)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Chức năng trích xuất PDF chưa được cấu hình trên máy chủ.");
            }

            var result = await _ocrService.ExtractAndParseAsync(tempPath);

            return Ok(new AutoFillDocumentResultDto
            {
                DocumentNumber = result.DocumentNumber,
                Title = result.Title,
                Summary = result.Summary,
                IssueDate = result.IssueDate,
                SenderName = result.SenderName,
                ReceiverName = result.ReceiverName,
                UrgencyLevel = result.UrgencyLevel,
                ContentText = result.ContentText,
                IsFromOcr = result.IsFromOcr
            });
        }
        finally
        {
            try
            {
                System.IO.File.Delete(tempPath);
            }
            catch
            {
                // Best effort cleanup for temporary upload files.
            }
        }
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

        var departmentScopeResult = RequireDepartmentAssignmentScope(request.ProcessingDepartment);

        if (departmentScopeResult != null)
        {
            return departmentScopeResult;
        }

        ApplyUpdate(existing, request);
        existing.UpdatedBy = User.GetUsername();

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

    [HttpPost("{id:long}/archive")]
    public async Task<IActionResult> Archive(long id)
    {
        var existing = await _documentService.GetByIdAsync(id);

        if (existing == null || !existing.IsActive)
        {
            return NotFound("Không tìm thấy văn bản.");
        }

        var permissionResult = RequireUpdatePermission(existing);

        if (permissionResult != null)
        {
            return permissionResult;
        }

        existing.Archive(User.GetUsername());
        existing.UpdatedBy = User.GetUsername();

        await _documentService.UpdateAsync(existing);

        return NoContent();
    }

    [HttpPost("{id:long}/restore-from-archive")]
    public async Task<IActionResult> RestoreFromArchive(long id)
    {
        var existing = await _documentService.GetByIdAsync(id);

        if (existing == null || !existing.IsActive)
        {
            return NotFound("Không tìm thấy văn bản.");
        }

        var permissionResult = RequireUpdatePermission(existing);

        if (permissionResult != null)
        {
            return permissionResult;
        }

        existing.MarkAsIssued(User.GetUsername());
        existing.UpdatedBy = User.GetUsername();

        await _documentService.UpdateAsync(existing);

        return NoContent();
    }

    private ActionResult? RequireCreatePermission()
    {
        var role = User.GetRole();

        if (string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized("Thiếu thông tin vai trò người dùng.");
        }

        if (User.HasAnyRole("Admin", "Manager", "Publisher", "Staff"))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền tạo văn bản.");
    }

    private ActionResult? RequireUpdatePermission(Document document)
    {
        var role = User.GetRole();

        if (string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized("Thiếu thông tin vai trò người dùng.");
        }

        if (User.HasAnyRole("Admin", "Manager", "Publisher"))
        {
            if (User.IsAdmin())
            {
                return null;
            }

            var userDepartment = User.GetDepartment();

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

    private bool CanReadDocument(Document document)
    {
        if (User.IsAdmin())
        {
            return true;
        }

        var username = User.GetUsername();

        if (!string.IsNullOrWhiteSpace(document.AssignedTo)
            && string.Equals(document.AssignedTo.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var userDepartment = User.GetDepartment();

        if (string.IsNullOrWhiteSpace(userDepartment))
        {
            return false;
        }

        return string.Equals(
            document.ProcessingDepartment?.Trim(),
            userDepartment.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyReadScope(DocumentManagement.Application.Models.DocumentSearchRequest request)
    {
        request.IsAdminScope = User.IsAdmin();

        if (request.IsAdminScope)
        {
            return;
        }

        request.ReadScopeUsername = User.GetUsername();
        request.ReadScopeDepartment = User.GetDepartment();
    }

    private static int NormalizePageSize(int pageSize)
    {
        if (pageSize <= 0)
        {
            return DefaultPageSize;
        }

        return Math.Min(pageSize, MaxPageSize);
    }

    private static bool IsAllowedPdfContentType(string? contentType)
    {
        return string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
               || string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> HasPdfSignatureAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var buffer = new byte[5];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

        return read == buffer.Length
               && buffer[0] == (byte)'%'
               && buffer[1] == (byte)'P'
               && buffer[2] == (byte)'D'
               && buffer[3] == (byte)'F'
               && buffer[4] == (byte)'-';
    }

    private ActionResult? RequireDeletePermission()
    {
        var role = User.GetRole();

        if (string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized("Thiếu thông tin vai trò người dùng.");
        }

        if (User.IsAdmin())
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền xóa văn bản.");
    }

    private ActionResult? RequireDepartmentAssignmentScope(string? requestedDepartment)
    {
        if (User.IsAdmin())
        {
            return null;
        }

        var userDepartment = User.GetDepartment();

        if (string.IsNullOrWhiteSpace(userDepartment))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Người dùng chưa được cấu hình phòng ban.");
        }

        if (string.Equals(
                requestedDepartment?.Trim(),
                userDepartment.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return StatusCode(
            StatusCodes.Status403Forbidden,
            "Bạn không được chuyển văn bản sang phòng ban khác.");
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
