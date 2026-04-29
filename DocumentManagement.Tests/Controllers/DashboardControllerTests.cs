using DocumentManagement.Api.Controllers;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.Dashboard;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DocumentManagement.Tests.Controllers;

public sealed class DashboardControllerTests
{
    private readonly Mock<IDocumentService> _documentService = new();

    [Fact]
    public async Task Get_ShouldBuildSummary_FromActiveIssuedDocuments()
    {
        var today = DateTime.Today;

        _documentService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<Document>
            {
                new()
                {
                    Id = 1,
                    DocumentNumber = "VB-001",
                    Title = "Issued effective",
                    StatusId = 4,
                    IsActive = true,
                    IssueDate = today.AddMonths(-1).ToString("yyyy-MM-dd"),
                    DueDate = today.AddDays(10).ToString("yyyy-MM-dd"),
                    ProcessingDepartment = "HCNS",
                    CreatedAt = today.AddDays(-5),
                    UpdatedAt = today.AddDays(-1)
                },
                new()
                {
                    Id = 2,
                    DocumentNumber = "VB-002",
                    Title = "Issued expired",
                    StatusId = 4,
                    IsActive = true,
                    IssueDate = today.ToString("yyyy-MM-dd"),
                    DueDate = today.AddDays(-1).ToString("yyyy-MM-dd"),
                    ProcessingDepartment = "HCNS",
                    CreatedAt = today.AddDays(-4),
                    UpdatedAt = today
                },
                new()
                {
                    Id = 3,
                    DocumentNumber = "VB-003",
                    Title = "Draft",
                    StatusId = 1,
                    IsActive = true,
                    ProcessingDepartment = "IT",
                    CreatedAt = today.AddDays(-3),
                    UpdatedAt = today.AddDays(-3)
                },
                new()
                {
                    Id = 4,
                    DocumentNumber = "VB-004",
                    Title = "Deleted document",
                    StatusId = 4,
                    IsActive = false,
                    ProcessingDepartment = "Deleted",
                    CreatedAt = today,
                    UpdatedAt = today
                }
            });

        var controller = new DashboardController(_documentService.Object);

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<DashboardDto>(ok.Value);

        Assert.Equal(3, dto.Summary.TotalDocuments);
        Assert.Equal(2, dto.Summary.IssuedDocuments);
        Assert.Equal(1, dto.Summary.EffectiveDocuments);
        Assert.Equal(1, dto.Summary.ExpiredDocuments);
        Assert.Equal("HCNS", dto.Summary.TopIssuingDepartment);
        Assert.Equal(2, dto.Summary.TopIssuingDepartmentCount);
    }

    [Fact]
    public async Task Get_ShouldReturnRecentDocuments_OrderedByLatestUpdate()
    {
        var now = DateTime.UtcNow;

        _documentService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<Document>
            {
                new()
                {
                    Id = 1,
                    DocumentNumber = "OLD",
                    Title = "Old",
                    StatusId = 4,
                    IsActive = true,
                    CreatedAt = now.AddDays(-10),
                    UpdatedAt = now.AddDays(-10)
                },
                new()
                {
                    Id = 2,
                    DocumentNumber = "NEW",
                    Title = "New",
                    StatusId = 4,
                    IsActive = true,
                    CreatedAt = now.AddDays(-5),
                    UpdatedAt = now
                },
                new()
                {
                    Id = 3,
                    DocumentNumber = "CREATED-FALLBACK",
                    Title = "Created fallback",
                    StatusId = 4,
                    IsActive = true,
                    CreatedAt = now.AddDays(-1),
                    UpdatedAt = default
                }
            });

        var controller = new DashboardController(_documentService.Object);

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<DashboardDto>(ok.Value);

        Assert.Equal("NEW", dto.RecentDocuments[0].DocumentNumber);
        Assert.Equal("CREATED-FALLBACK", dto.RecentDocuments[1].DocumentNumber);
        Assert.Equal("OLD", dto.RecentDocuments[2].DocumentNumber);
    }

    [Fact]
    public async Task Get_ShouldBuildEffectivenessChart_OnlyForIssuedDocuments()
    {
        var today = DateTime.Today;

        _documentService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<Document>
            {
                new()
                {
                    Id = 1,
                    StatusId = 4,
                    IsActive = true,
                    DueDate = today.AddDays(5).ToString("yyyy-MM-dd")
                },
                new()
                {
                    Id = 2,
                    StatusId = 4,
                    IsActive = true,
                    IsExpired = true
                },
                new()
                {
                    Id = 3,
                    StatusId = 1,
                    IsActive = true,
                    IsExpired = true
                }
            });

        var controller = new DashboardController(_documentService.Object);

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<DashboardDto>(ok.Value);

        Assert.Contains(dto.EffectivenessChart, x => x.Name == "Có hiệu lực" && x.Value == 1);
        Assert.Contains(dto.EffectivenessChart, x => x.Name == "Hết hiệu lực" && x.Value == 1);
    }
}
