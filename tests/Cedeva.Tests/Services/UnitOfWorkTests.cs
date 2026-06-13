using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Services;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Services;

public class UnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_PersistsTrackedEntities()
    {
        using var db = new SqliteTestContext();
        var org = TestData.Organisation();
        db.Context.Add(org);

        var sut = new UnitOfWork(db.Context);
        await sut.SaveChangesAsync();

        await using var verify = db.NewContext();
        (await verify.Organisations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SaveChangesAsync_ReturnsNumberOfAffectedRows()
    {
        using var db = new SqliteTestContext();
        // Organisation owns an Address (owned type) -> persisting one org affects >1 rows,
        // so use a stand-alone count assertion rather than a hard-coded number for the org.
        var org = TestData.Organisation();
        db.Context.Add(org);
        var sut = new UnitOfWork(db.Context);

        var affected = await sut.SaveChangesAsync();

        affected.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoChanges_ReturnsZero()
    {
        using var db = new SqliteTestContext();
        var sut = new UnitOfWork(db.Context);

        var affected = await sut.SaveChangesAsync();

        affected.Should().Be(0);
    }

    [Fact]
    public async Task SaveChangesAsync_PopulatesAuditFieldsOnInsert()
    {
        using var db = new SqliteTestContext();
        var before = DateTime.UtcNow.AddSeconds(-1);
        var org = TestData.Organisation();
        db.Context.Add(org);

        var sut = new UnitOfWork(db.Context);
        await sut.SaveChangesAsync();

        await using var verify = db.NewContext();
        var saved = await verify.Organisations.SingleAsync();
        saved.CreatedAt.Should().BeOnOrAfter(before);
        saved.CreatedBy.Should().Be("test-user"); // default FakeCurrentUserService.UserId
        saved.ModifiedAt.Should().BeNull();
        saved.ModifiedBy.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_PopulatesModifiedFieldsOnUpdate_AndPreservesCreated()
    {
        using var db = new SqliteTestContext();
        var org = TestData.Organisation();
        db.Context.Add(org);
        await new UnitOfWork(db.Context).SaveChangesAsync();

        var originalCreatedAt = org.CreatedAt;
        var originalCreatedBy = org.CreatedBy;

        // Modify through a fresh context + a fresh UnitOfWork.
        await using var editCtx = db.NewContext();
        var toEdit = await editCtx.Organisations.SingleAsync();
        toEdit.Name = "Renamed Org";
        await new UnitOfWork(editCtx).SaveChangesAsync();

        await using var verify = db.NewContext();
        var saved = await verify.Organisations.SingleAsync();
        saved.Name.Should().Be("Renamed Org");
        saved.ModifiedAt.Should().NotBeNull();
        saved.ModifiedBy.Should().Be("test-user");
        saved.CreatedAt.Should().Be(originalCreatedAt);
        saved.CreatedBy.Should().Be(originalCreatedBy);
    }

    [Fact]
    public async Task SaveChangesAsync_CommitsAcrossMultipleCalls()
    {
        using var db = new SqliteTestContext();
        var sut = new UnitOfWork(db.Context);

        db.Context.Add(TestData.Organisation("Org One"));
        await sut.SaveChangesAsync();

        db.Context.Add(TestData.Organisation("Org Two"));
        await sut.SaveChangesAsync();

        await using var verify = db.NewContext();
        (await verify.Organisations.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Dispose_DisposesUnderlyingContext()
    {
        using var db = new SqliteTestContext();
        var ctx = db.NewContext(); // separate context so we don't disrupt db.Context

        var sut = new UnitOfWork(ctx);
        sut.Dispose();

        // A disposed DbContext throws when used.
        var act = async () => await ctx.Organisations.CountAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        using var db = new SqliteTestContext();
        var ctx = db.NewContext();
        var sut = new UnitOfWork(ctx);

        sut.Dispose();
        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task InterfaceSaveChangesAsync_DelegatesToContext()
    {
        using var db = new SqliteTestContext();
        var org = TestData.Organisation();
        db.Context.Add(org);

        IUnitOfWork sut = new UnitOfWork(db.Context);
        var affected = await sut.SaveChangesAsync();

        affected.Should().BeGreaterThan(0);
        await using var verify = db.NewContext();
        (await verify.Organisations.CountAsync()).Should().Be(1);
    }
}
