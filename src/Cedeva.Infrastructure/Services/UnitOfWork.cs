using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;

namespace Cedeva.Infrastructure.Services;

public class UnitOfWork : IUnitOfWork
{
    private readonly CedevaDbContext _context;

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
        _context.Dispose();
    }
}
