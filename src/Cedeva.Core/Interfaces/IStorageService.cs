namespace Cedeva.Core.Interfaces;

public interface IStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string containerPath);
    Task<Stream?> DownloadFileAsync(string filePath);
    Task DeleteFileAsync(string filePath);
    string GetFileUrl(string filePath);
}
