using System.Security.Claims;
using DocumentManagement.Api.Controllers;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using AppDocumentSearchRequest = DocumentManagement.Application.Models.DocumentSearchRequest;
using AppPagedResult = DocumentManagement.Application.Models.PagedResult<DocumentManagement.Domain.Entities.Document>;
using ContractCreateDocumentRequest = DocumentManagement.Contracts.Documents.CreateDocumentRequest;
using ContractDocumentDto = DocumentManagement.Contracts.Documents.DocumentDto;
using ContractUpdateDocumentRequest = DocumentManagement.Contracts.Documents.UpdateDocumentRequest;

namespace DocumentManagement.Tests.Controllers;

public class DocumentsControllerTests
{
    private readonly Mock<IDocumentService> _mockDocumentService;
    private readonly DocumentsController _controller;

    public DocumentsControllerTests()
    {
        _mockDocumentService = new Mock<IDocumentService>();
        _controller = CreateController("Admin");
    }

    private DocumentsController CreateController(string role, string department = "Phòng HCNS")
    {
        var controller = new DocumentsController(_mockDocumentService.Object);

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.Name, "test-user"),
                    new Claim(ClaimTypes.Role, role),
                    new Claim("department", department)
                },
                "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetAll_ShouldReturnActiveDocuments()
    {
        var documents = new List<Document>
        {
            new() { Id = 1, Title = "Doc 1", IsActive = true },
            new() { Id = 2, Title = "Doc 2", IsActive = true }
        };

        _mockDocumentService
            .Setup(s => s.GetAllAsync())
            .ReturnsAsync(documents);

        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var documentsResult = Assert.IsType<List<ContractDocumentDto>>(okResult.Value);

        Assert.Equal(2, documentsResult.Count);
    }

    [Fact]
    public async Task GetById_ShouldReturnDocument_WhenExists()
    {
        var document = new Document
        {
            Id = 1,
            Title = "Test Doc",
            IsActive = true
        };

        _mockDocumentService
            .Setup(s => s.GetByIdAsync(1))
            .ReturnsAsync(document);

        var result = await _controller.GetById(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);

        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenNotExists()
    {
        _mockDocumentService
            .Setup(s => s.GetByIdAsync(999))
            .ReturnsAsync((Document?)null);

        var result = await _controller.GetById(999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenDocumentNumberEmpty()
    {
        var controller = CreateController("Staff");

        var request = new ContractCreateDocumentRequest
        {
            DocumentNumber = "",
            Title = "Test Title"
        };

        var result = await controller.Create(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);

        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenTitleEmpty()
    {
        var controller = CreateController("Staff");

        var request = new ContractCreateDocumentRequest
        {
            DocumentNumber = "001",
            Title = ""
        };

        var result = await controller.Create(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);

        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Search_ShouldReturnPagedResults()
    {
        var pagedResult = new AppPagedResult
        {
            Items = new List<Document>
            {
                new() { Id = 1, Title = "Test", IsActive = true }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 10
        };

        _mockDocumentService
            .Setup(s => s.SearchPagedAsync(It.IsAny<AppDocumentSearchRequest>()))
            .ReturnsAsync(pagedResult);

        var result = await _controller.Search(
            keyword: "test",
            categoryId: null,
            statusId: null,
            urgency: null,
            fromDate: null,
            toDate: null);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);

        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenIdsMismatch()
    {
        var controller = CreateController("Manager");

        var request = new ContractUpdateDocumentRequest
        {
            Id = 2
        };

        var result = await controller.Update(1, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);

        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenNotExists()
    {
        var controller = CreateController("Admin");

        _mockDocumentService
            .Setup(s => s.GetByIdAsync(999))
            .ReturnsAsync((Document?)null);

        var result = await controller.Delete(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_ShouldAllowPublisher()
    {
        var controller = CreateController("Publisher");

        var existing = new Document
        {
            Id = 7,
            DocumentNumber = "PUB-001",
            Title = "Original",
            ProcessingDepartment = "Phòng HCNS",
            IsActive = true
        };

        _mockDocumentService
            .Setup(s => s.GetByIdAsync(7))
            .ReturnsAsync(existing);

        _mockDocumentService
            .Setup(s => s.UpdateAsync(It.IsAny<Document>()))
            .Returns(Task.CompletedTask);

        var request = new ContractUpdateDocumentRequest
        {
            Id = 7,
            DocumentNumber = "PUB-001",
            Title = "Publisher updated",
            ProcessingDepartment = "Phòng HCNS",
            StatusId = 4
        };

        var result = await controller.Update(7, request);

        Assert.IsType<NoContentResult>(result);
        _mockDocumentService.Verify(s => s.UpdateAsync(It.IsAny<Document>()), Times.Once);
    }

    [Fact]
    public async Task Update_ShouldBlockPublisher_WhenDepartmentMismatch()
    {
        var controller = CreateController("Publisher", "Phòng Kế toán");

        var existing = new Document
        {
            Id = 8,
            DocumentNumber = "PUB-002",
            Title = "Original",
            ProcessingDepartment = "Phòng HCNS",
            IsActive = true
        };

        _mockDocumentService
            .Setup(s => s.GetByIdAsync(8))
            .ReturnsAsync(existing);

        var request = new ContractUpdateDocumentRequest
        {
            Id = 8,
            DocumentNumber = "PUB-002",
            Title = "Blocked",
            ProcessingDepartment = "Phòng HCNS",
            StatusId = 4
        };

        var result = await controller.Update(8, request);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        _mockDocumentService.Verify(s => s.UpdateAsync(It.IsAny<Document>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ShouldBlockPublisher()
    {
        var controller = CreateController("Publisher");

        var result = await controller.Delete(7);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }
}
