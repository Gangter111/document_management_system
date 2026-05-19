using DocumentManagement.Infrastructure.Services;
using Xunit;

namespace DocumentManagement.Tests.Services;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dms-storage-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveFileAsync_UsesHashBasedNameAndStoresInsideRoot()
    {
        var service = new LocalFileStorageService(_root);
        var source = Path.Combine(_root, "source.pdf");
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(source, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 });

        var saved = await service.SaveFileAsync(source);

        Assert.Equal(64 + ".pdf".Length, saved.StoredFileName.Length);
        Assert.StartsWith(Path.GetFullPath(_root), saved.StoredFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(saved.StoredFilePath));
        Assert.Equal(saved.FileHash + ".pdf", saved.StoredFileName);
    }

    [Fact]
    public async Task SaveFileAsync_RejectsExtensionSpoofing()
    {
        var service = new LocalFileStorageService(_root);
        var source = Path.Combine(_root, "spoof.pdf");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(source, "not a pdf");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveFileAsync(source));
    }

    [Fact]
    public void DeleteFile_RejectsPathsOutsideStorageRoot()
    {
        var service = new LocalFileStorageService(_root);
        var outside = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(outside, new byte[] { 0x25, 0x50, 0x44, 0x46 });

        try
        {
            Assert.Throws<InvalidOperationException>(() => service.DeleteFile(outside));
            Assert.True(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
