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
        // Organisation-scoped path: uploads/{organisationId}/{folder}/{guid}_{filename}
        var relativePath = string.IsNullOrEmpty(containerPath)
            ? Path.Combine(UploadFolder, fileName)
            : Path.Combine(UploadFolder, containerPath, fileName);

        var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

        // Créer répertoire si inexistant
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Sauvegarder fichier
        using (var fileStreamOut = new FileStream(fullPath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fileStreamOut);
        }

        // Retourner chemin relatif avec slash
        return "/" + relativePath.Replace("\\", "/");
    }

    public Task<Stream?> DownloadFileAsync(string filePath)
    {
        var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read));
    }

    public Task DeleteFileAsync(string filePath)
    {
        var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
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
