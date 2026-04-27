using System.Globalization;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.Dashboard;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DashboardController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var documents = (await _documentService.GetAllAsync())
            .Where(document => document.IsActive)
            .ToList();

        var issuedDocuments = documents
            .Where(IsIssuedDocument)
            .ToList();

        var dto = new DashboardDto
        {
            Summary = BuildSummary(documents, issuedDocuments),
            RecentDocuments = BuildRecentDocuments(documents),
            EffectivenessChart = BuildEffectivenessChart(issuedDocuments),
            MonthlyIssuedChart = BuildMonthlyIssuedChart(issuedDocuments),
            DepartmentIssuedChart = BuildDepartmentIssuedChart(issuedDocuments)
        };

        return Ok(dto);
    }

    private static DashboardSummaryDto BuildSummary(
        IReadOnlyList<Document> documents,
        IReadOnlyList<Document> issuedDocuments)
    {
        var effectiveDocuments = issuedDocuments.Count(document => !IsExpiredDocument(document));
        var expiredDocuments = issuedDocuments.Count(IsExpiredDocument);
        var departmentChart = BuildDepartmentIssuedChart(issuedDocuments);

        var topDepartment = departmentChart
            .OrderByDescending(item => item.Value)
            .FirstOrDefault();

        return new DashboardSummaryDto
        {
            TotalDocuments = documents.Count,
            IssuedDocuments = issuedDocuments.Count,
            EffectiveDocuments = effectiveDocuments,
            ExpiredDocuments = expiredDocuments,
            TopIssuingDepartment = topDepartment?.Name ?? "Chưa có dữ liệu",
            TopIssuingDepartmentCount = topDepartment?.Value ?? 0,
            AverageIssuedPerMonth = CalculateAverageIssuedPerMonth(issuedDocuments)
        };
    }

    private static List<RecentDocumentDto> BuildRecentDocuments(IReadOnlyList<Document> documents)
    {
        return documents
            .OrderByDescending(document => document.UpdatedAt == default ? document.CreatedAt : document.UpdatedAt)
            .Take(10)
            .Select(document => new RecentDocumentDto
            {
                Id = document.Id,
                DocumentNumber = document.DocumentNumber,
                Title = document.Title,
                StatusText = document.StatusText,
                UrgencyText = document.UrgencyText,
                DueDate = document.DueDate,
                DocumentDate = document.DocumentDate,
                UpdatedAt = document.UpdatedAt == default ? document.CreatedAt : document.UpdatedAt
            })
            .ToList();
    }

    private static List<DashboardChartItemDto> BuildEffectivenessChart(IReadOnlyList<Document> issuedDocuments)
    {
        var effectiveCount = issuedDocuments.Count(document => !IsExpiredDocument(document));
        var expiredCount = issuedDocuments.Count(IsExpiredDocument);

        return new List<DashboardChartItemDto>
            {
                new()
                {
                    Name = "Có hiệu lực",
                    Value = effectiveCount
                },
                new()
                {
                    Name = "Hết hiệu lực",
                    Value = expiredCount
                }
            }
            .Where(item => item.Value > 0)
            .ToList();
    }

    private static List<DashboardChartItemDto> BuildMonthlyIssuedChart(IReadOnlyList<Document> issuedDocuments)
    {
        var now = DateTime.Today;
        var startMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

        var monthKeys = Enumerable.Range(0, 12)
            .Select(offset => startMonth.AddMonths(offset))
            .ToList();

        var grouped = issuedDocuments
            .Select(document => TryParseDocumentDate(document, out var date)
                ? new DateTime(date.Year, date.Month, 1)
                : (DateTime?)null)
            .Where(date => date.HasValue && date.Value >= startMonth)
            .GroupBy(date => date!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        return monthKeys
            .Select(month => new DashboardChartItemDto
            {
                Name = month.ToString("MM/yyyy", CultureInfo.InvariantCulture),
                Value = grouped.TryGetValue(month, out var count) ? count : 0
            })
            .ToList();
    }

    private static List<DashboardChartItemDto> BuildDepartmentIssuedChart(IReadOnlyList<Document> issuedDocuments)
    {
        return issuedDocuments
            .GroupBy(GetIssuingDepartment)
            .Select(group => new DashboardChartItemDto
            {
                Name = group.Key,
                Value = group.Count()
            })
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Name)
            .Take(8)
            .ToList();
    }

    private static double CalculateAverageIssuedPerMonth(IReadOnlyList<Document> issuedDocuments)
    {
        var dates = issuedDocuments
            .Select(document => TryParseDocumentDate(document, out var date) ? date : (DateTime?)null)
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .ToList();

        if (dates.Count == 0)
        {
            return 0;
        }

        var minMonth = new DateTime(dates.Min().Year, dates.Min().Month, 1);
        var maxMonth = new DateTime(dates.Max().Year, dates.Max().Month, 1);

        var monthCount = ((maxMonth.Year - minMonth.Year) * 12) + maxMonth.Month - minMonth.Month + 1;

        if (monthCount <= 0)
        {
            return 0;
        }

        return Math.Round((double)issuedDocuments.Count / monthCount, 1);
    }

    private static bool IsIssuedDocument(Document document)
    {
        return document.StatusId == 4;
    }

    private static bool IsExpiredDocument(Document document)
    {
        if (document.IsExpired)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(document.DueDate))
        {
            return false;
        }

        return DateTime.TryParse(document.DueDate, out var dueDate)
               && dueDate.Date < DateTime.Today;
    }

    private static string GetIssuingDepartment(Document document)
    {
        if (!string.IsNullOrWhiteSpace(document.ProcessingDepartment))
        {
            return document.ProcessingDepartment.Trim();
        }

        if (!string.IsNullOrWhiteSpace(document.SenderName))
        {
            return document.SenderName.Trim();
        }

        return "Chưa xác định";
    }

    private static bool TryParseDocumentDate(Document document, out DateTime date)
    {
        if (!string.IsNullOrWhiteSpace(document.IssueDate)
            && DateTime.TryParse(document.IssueDate, out date))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(document.ReceivedDate)
            && DateTime.TryParse(document.ReceivedDate, out date))
        {
            return true;
        }

        date = document.CreatedAt == default
            ? DateTime.Today
            : document.CreatedAt;

        return true;
    }
}