using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace Cedeva.Infrastructure.Services.Storage;

public class LocalFileStorageService : IStorageService
{
    private readonly IWebHostEnvironment _environment;
    private const string UploadFolder = "uploads";

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string containerPath)
    {
        // Security: Strip any directory components from fileName to prevent path traversal
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safeFileName))
        {
            throw new ArgumentException("Invalid file name", nameof(fileName));
        }

        // Security: Validate containerPath doesn't contain path traversal sequences
        if (!string.IsNullOrEmpty(containerPath) &&
            (containerPath.Contains("..", StringComparison.Ordinal) ||
             containerPath.Contains('/', StringComparison.Ordinal) ||
             containerPath.Contains('\\', StringComparison.Ordinal)))
        {
            throw new ArgumentException("Container path contains invalid characters", nameof(containerPath));
        }

        // Organisation-scoped path: uploads/{organisationId}/{folder}/{guid}_{filename}
        var relativePath = string.IsNullOrEmpty(containerPath)
            ? Path.Combine(UploadFolder, safeFileName)
            : Path.Combine(UploadFolder, containerPath, safeFileName);

        var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

        // Security: Verify the resolved path is within the WebRootPath
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedWebRoot = Path.GetFullPath(_environment.WebRootPath);
        if (!normalizedFullPath.StartsWith(normalizedWebRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path traversal attempt detected");
        }

        // Créer répertoire si inexistant
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Sauvegarder fichier
        await using (var fileStreamOut = new FileStream(fullPath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fileStreamOut);
        }

        // Retourner chemin relatif avec slash
        return "/" + relativePath.Replace("\\", "/");
    }

    public Task<Stream?> DownloadFileAsync(string filePath)
    {
        var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));

        // Security: Verify the resolved path is within the WebRootPath
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedWebRoot = Path.GetFullPath(_environment.WebRootPath);
        if (!normalizedFullPath.StartsWith(normalizedWebRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<Stream?>(null);
        }

        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read));
    }

    public Task DeleteFileAsync(string filePath)
    {
        var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));

        // Security: Verify the resolved path is within the WebRootPath
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedWebRoot = Path.GetFullPath(_environment.WebRootPath);
        if (!normalizedFullPath.StartsWith(normalizedWebRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetFileUrl(string filePath)
    {
        // Retourner chemin relatif (déjà au bon format)
        return filePath;
    }
}
