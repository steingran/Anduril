using Anduril.App.Models;

namespace Anduril.App.Tests;

public sealed class DiffLineTests
{
    [Test]
    public async Task DiffLine_RecordEquality_BasedOnKindAndContent()
    {
        var a = new DiffLine(DiffLineKind.Added, "+ new line");
        var b = new DiffLine(DiffLineKind.Added, "+ new line");
        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task DiffLine_RecordInequality_WhenKindDiffers()
    {
        var a = new DiffLine(DiffLineKind.Added, "line");
        var b = new DiffLine(DiffLineKind.Removed, "line");
        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task DiffLine_RecordInequality_WhenContentDiffers()
    {
        var a = new DiffLine(DiffLineKind.Context, "old");
        var b = new DiffLine(DiffLineKind.Context, "new");
        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    [Arguments(DiffLineKind.Added)]
    [Arguments(DiffLineKind.Removed)]
    [Arguments(DiffLineKind.Context)]
    public async Task DiffLine_AllKinds_CanBeCreated(DiffLineKind kind)
    {
        var line = new DiffLine(kind, "content");
        await Assert.That(line.Kind).IsEqualTo(kind);
        await Assert.That(line.Content).IsEqualTo("content");
    }
}
