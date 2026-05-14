using System.Security.Claims;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.DemoData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/demo-data")]
[Authorize]
public class DemoDataController : ControllerBase
{
    private readonly IDemoDataService _demoDataService;

    public DemoDataController(IDemoDataService demoDataService)
    {
        _demoDataService = demoDataService;
    }

    [HttpGet("count")]
    public async Task<ActionResult<DemoDataResponse>> Count(CancellationToken cancellationToken)
    {
        var count = await _demoDataService.GetActiveDemoCountAsync(cancellationToken);
        return Ok(new DemoDataResponse
        {
            ActiveDemoCount = count,
            Message = "Demo data count loaded."
        });
    }

    [HttpPost("seed")]
    public async Task<ActionResult<DemoDataResponse>> Seed(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin();
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var affected = await _demoDataService.EnsureSeededAsync(cancellationToken);
        var count = await _demoDataService.GetActiveDemoCountAsync(cancellationToken);

        return Ok(new DemoDataResponse
        {
            ActiveDemoCount = count,
            AffectedCount = affected,
            Message = affected == 0
                ? "Demo data is already present."
                : "Demo data seeded."
        });
    }

    [HttpDelete]
    public async Task<ActionResult<DemoDataResponse>> Clear(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin();
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var affected = await _demoDataService.ClearAsync(cancellationToken);
        var count = await _demoDataService.GetActiveDemoCountAsync(cancellationToken);

        return Ok(new DemoDataResponse
        {
            ActiveDemoCount = count,
            AffectedCount = affected,
            Message = "Demo data cleared."
        });
    }

    private ActionResult? RequireAdmin()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value
                   ?? User.FindFirst("role")?.Value
                   ?? string.Empty;

        return string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, "Chỉ Admin được quản lý demo data.");
    }
}
