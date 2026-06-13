using System.Text;
using Cedeva.Infrastructure.Services.Storage;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;

namespace Cedeva.Tests.Services.Storage;

/// <summary>
/// Unit tests for <see cref="LocalFileStorageService"/>. The service writes to a real
/// filesystem rooted at <see cref="IWebHostEnvironment.WebRootPath"/>, which is mocked to a
/// unique temp directory that is cleaned up after each test.
/// </summary>
public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _webRoot;
    private readonly IWebHostEnvironment _environment;
    private readonly LocalFileStorageService _sut;

    public LocalFileStorageServiceTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), "cedeva-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_webRoot);

        _environment = Substitute.For<IWebHostEnvironment>();
        _environment.WebRootPath.Returns(_webRoot);

        _sut = new LocalFileStorageService(_environment);
    }

    private static Stream StreamFrom(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));

    // ---------------------------------------------------------------------
    // UploadFileAsync — happy path
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Upload_WithContainerPath_WritesFileAndReturnsRelativeUrl()
    {
        var result = await _sut.UploadFileAsync(StreamFrom("hello"), "report.pdf", "application/pdf", "1");

        result.Should().Be("/uploads/1/report.pdf");

        var onDisk = Path.Combine(_webRoot, "uploads", "1", "report.pdf");
        File.Exists(onDisk).Should().BeTrue();
        (await File.ReadAllTextAsync(onDisk)).Should().Be("hello");
    }

    [Fact]
    public async Task Upload_WithoutContainerPath_WritesDirectlyUnderUploads()
    {
        var result = await _sut.UploadFileAsync(StreamFrom("data"), "logo.png", "image/png", "");

        result.Should().Be("/uploads/logo.png");
        File.Exists(Path.Combine(_webRoot, "uploads", "logo.png")).Should().BeTrue();
    }

    [Fact]
    public async Task Upload_ReturnsUrlWithForwardSlashes()
    {
        var result = await _sut.UploadFileAsync(StreamFrom("x"), "file.txt", "text/plain", "42");

        result.Should().NotContain("\\");
        result.Should().Be("/uploads/42/file.txt");
    }

    [Fact]
    public async Task Upload_CreatesMissingDirectories()
    {
        Directory.Exists(Path.Combine(_webRoot, "uploads", "99")).Should().BeFalse();

        await _sut.UploadFileAsync(StreamFrom("x"), "a.txt", "text/plain", "99");

        Directory.Exists(Path.Combine(_webRoot, "uploads", "99")).Should().BeTrue();
    }

    [Fact]
    public async Task Upload_StripsDirectoryComponentsFromFileName()
    {
        // Only the bare file name should be used; the leading path is discarded.
        var result = await _sut.UploadFileAsync(StreamFrom("x"), "../../evil.txt", "text/plain", "1");

        result.Should().Be("/uploads/1/evil.txt");
        File.Exists(Path.Combine(_webRoot, "uploads", "1", "evil.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task Upload_OverwritesExistingFile()
    {
        await _sut.UploadFileAsync(StreamFrom("first"), "f.txt", "text/plain", "1");
        await _sut.UploadFileAsync(StreamFrom("second"), "f.txt", "text/plain", "1");

        var onDisk = Path.Combine(_webRoot, "uploads", "1", "f.txt");
        (await File.ReadAllTextAsync(onDisk)).Should().Be("second");
    }

    // ---------------------------------------------------------------------
    // UploadFileAsync — invalid input
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Upload_FileNameThatResolvesToEmpty_Throws()
    {
        // Path.GetFileName("foo/") => "" which the service rejects.
        var act = async () => await _sut.UploadFileAsync(StreamFrom("x"), "foo/", "text/plain", "1");

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Invalid file name*");
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../secret")]
    [InlineData("sub/dir")]
    [InlineData("sub\\dir")]
    [InlineData("a..b")]
    public async Task Upload_ContainerPathWithInvalidCharacters_Throws(string containerPath)
    {
        var act = async () => await _sut.UploadFileAsync(StreamFrom("x"), "f.txt", "text/plain", containerPath);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Container path contains invalid characters*");
    }

    // ---------------------------------------------------------------------
    // DownloadFileAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Download_ExistingFile_ReturnsStreamWithContent()
    {
        var url = await _sut.UploadFileAsync(StreamFrom("payload"), "doc.txt", "text/plain", "1");

        await using var stream = await _sut.DownloadFileAsync(url);

        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!);
        (await reader.ReadToEndAsync()).Should().Be("payload");
    }

    [Fact]
    public async Task Download_MissingFile_ReturnsNull()
    {
        (await _sut.DownloadFileAsync("/uploads/1/does-not-exist.txt")).Should().BeNull();
    }

    [Theory]
    [InlineData("/../../secret.txt")]
    [InlineData("/..\\..\\secret.txt")]
    public async Task Download_PathTraversal_ReturnsNull(string filePath)
    {
        (await _sut.DownloadFileAsync(filePath)).Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // DeleteFileAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Delete_ExistingFile_RemovesIt()
    {
        var url = await _sut.UploadFileAsync(StreamFrom("x"), "del.txt", "text/plain", "1");
        var onDisk = Path.Combine(_webRoot, "uploads", "1", "del.txt");
        File.Exists(onDisk).Should().BeTrue();

        await _sut.DeleteFileAsync(url);

        File.Exists(onDisk).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_MissingFile_DoesNotThrow()
    {
        var act = async () => await _sut.DeleteFileAsync("/uploads/1/nope.txt");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Delete_PathTraversal_DoesNotDeleteOutsideWebRootAndDoesNotThrow()
    {
        // Create a file OUTSIDE the web root that a traversal would target.
        var outsideDir = Path.Combine(Path.GetTempPath(), "cedeva-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "victim.txt");
        await File.WriteAllTextAsync(outsideFile, "keep me");

        try
        {
            // A traversal path that would resolve outside the web root must be ignored.
            var act = async () => await _sut.DeleteFileAsync("/../" + Path.GetFileName(outsideDir) + "/victim.txt");

            await act.Should().NotThrowAsync();
            File.Exists(outsideFile).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    // ---------------------------------------------------------------------
    // GetFileUrl
    // ---------------------------------------------------------------------

    [Fact]
    public void GetFileUrl_ReturnsPathUnchanged()
    {
        _sut.GetFileUrl("/uploads/1/file.txt").Should().Be("/uploads/1/file.txt");
    }

    // ---------------------------------------------------------------------
    // Round-trip
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UploadReadDelete_RoundTrip()
    {
        var url = await _sut.UploadFileAsync(StreamFrom("round-trip"), "rt.txt", "text/plain", "7");

        await using (var stream = await _sut.DownloadFileAsync(url))
        {
            stream.Should().NotBeNull();
            using var reader = new StreamReader(stream!);
            (await reader.ReadToEndAsync()).Should().Be("round-trip");
        }

        await _sut.DeleteFileAsync(url);

        (await _sut.DownloadFileAsync(url)).Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }
}
