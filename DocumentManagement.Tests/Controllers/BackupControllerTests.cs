using System.Security.Claims;
using DocumentManagement.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace DocumentManagement.Tests.Controllers;

public sealed class BackupControllerTests
{
    [Fact]
    public async Task Download_ShouldRejectNonAdmin()
    {
        var controller = CreateController("Staff");

        var result = await controller.Download(CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Restore_ShouldRejectNonAdmin()
    {
        var controller = CreateController("Staff");
        await using var stream = new MemoryStream("not sqlite"u8.ToArray());
        var file = new FormFile(stream, 0, stream.Length, "file", "backup.db");

        var result = await controller.Restore(file, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Restore_ShouldRejectUnsupportedExtension()
    {
        var controller = CreateController("Admin");
        await using var stream = new MemoryStream("not sqlite"u8.ToArray());
        var file = new FormFile(stream, 0, stream.Length, "file", "backup.zip");

        var result = await controller.Restore(file, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public void Health_ShouldRejectNonAdmin()
    {
        var controller = CreateController("Staff");

        var result = controller.Health();

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private static BackupController CreateController(string role)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:Path"] = Path.Combine(Path.GetTempPath(), "QuanLyVanBan.Tests", $"{Guid.NewGuid():N}.db")
            })
            .Build();

        var environment = new Mock<IWebHostEnvironment>();
        environment
            .Setup(x => x.ContentRootPath)
            .Returns(Path.GetTempPath());

        var controller = new BackupController(configuration, environment.Object);

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Name, "test-user"),
                    new Claim(ClaimTypes.Role, role)
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
}
