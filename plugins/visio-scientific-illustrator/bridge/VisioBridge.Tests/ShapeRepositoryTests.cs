using VisioBridge.Core;
using VisioBridge.Core.Shapes;
using VisioBridge.Tests.Fakes;

namespace VisioBridge.Tests;

public sealed class ShapeRepositoryTests
{
    [Fact]
    public void Resolve_rejects_duplicate_semantic_ids()
    {
        var accessor = FakeShapeAccessor.WithShapes(
            new FakeShape(1, "encoder"),
            new FakeShape(2, "encoder"));
        var repository = new ShapeRepository(accessor);

        var error = Assert.Throws<BridgeCommandException>(() => repository.Resolve("encoder"));

        Assert.Equal(BridgeErrorCodes.DuplicateSemanticId, error.Code);
    }

    [Fact]
    public void Register_writes_User_CodexId_and_NameU()
    {
        var shape = new FakeShape(3, null);
        var repository = new ShapeRepository(FakeShapeAccessor.WithShapes(shape));

        repository.Register(shape, "encoder block");

        Assert.Equal("encoder block", shape.UserCodexId);
        Assert.Equal("Codex_encoder_block", shape.NameU);
    }

    [Fact]
    public void Resolve_returns_the_exact_semantic_id()
    {
        var expected = new FakeShape(2, "encoder");
        var repository = new ShapeRepository(FakeShapeAccessor.WithShapes(
            new FakeShape(1, "encoder-2"),
            expected));

        using var resolved = repository.Resolve("encoder");

        Assert.Equal(expected.Id, resolved.Id);
    }

    [Fact]
    public void Register_rejects_universal_name_collisions()
    {
        var first = new FakeShape(1, "a-b") { NameU = "Codex_a_b" };
        var second = new FakeShape(2, null);
        var repository = new ShapeRepository(FakeShapeAccessor.WithShapes(first, second));

        var error = Assert.Throws<BridgeCommandException>(() =>
            repository.Register(second, "a b"));

        Assert.Equal(BridgeErrorCodes.DuplicateSemanticId, error.Code);
    }
}
