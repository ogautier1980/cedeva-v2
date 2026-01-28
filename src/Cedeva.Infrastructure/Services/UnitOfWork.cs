using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;

namespace Cedeva.Infrastructure.Services;

public class UnitOfWork : IUnitOfWork
{
    private readonly CedevaDbContext _context;
    private bool _disposed;

    public UnitOfWork(CedevaDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            _disposed = true;
        }
    }
}
