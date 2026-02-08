using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace Cedeva.Website.Infrastructure;

public interface IStorageContext
{
    IStorageService Storage { get; }
    IWebHostEnvironment WebHost { get; }
}

public class StorageContext : IStorageContext
{
    public IStorageService Storage { get; }
    public IWebHostEnvironment WebHost { get; }

    public StorageContext(IStorageService storage, IWebHostEnvironment webHost)
    {
        Storage = storage;
        WebHost = webHost;
    }
}
