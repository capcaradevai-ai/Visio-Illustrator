using VisioTestBridge.Core;
using VisioTestBridge.Core.Shapes;
using VisioTestBridge.Tests.Fakes;

namespace VisioTestBridge.Tests;

public sealed class ShapeRepositoryTests
{
    [Fact]
    public void Resolve_rejects_an_unindexed_semantic_id()
    {
        var accessor = FakeShapeAccessor.WithShapes(new FakeShape(1, "other")
        {
            NameU = "Codex_encoder",
        });
        var repository = new ShapeRepository(accessor);

        var error = Assert.Throws<BridgeCommandException>(() => repository.Resolve("encoder"));

        Assert.Equal(BridgeErrorCodes.ShapeNotFound, error.Code);
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
        var expected = new FakeShape(2, "encoder") { NameU = "Codex_encoder" };
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

    [Fact]
    public void Register_builds_the_semantic_index_only_once()
    {
        var shape = new FakeShape(3, null);
        var second = new FakeShape(4, null);
        var accessor = FakeShapeAccessor.WithShapes(shape, second);
        var repository = new ShapeRepository(accessor);

        repository.Register(shape, "new_shape");
        repository.Register(second, "second_shape");

        Assert.Equal(1, accessor.EnumerationCount);
        Assert.Equal(0, accessor.UniversalNameLookupCount);
    }

    [Fact]
    public void Resolve_uses_direct_NameU_lookup_without_enumerating_the_page()
    {
        var shape = new FakeShape(3, "target") { NameU = "Codex_target" };
        var accessor = FakeShapeAccessor.WithShapes(shape);
        var repository = new ShapeRepository(accessor);

        using var resolved = repository.Resolve("target");

        Assert.Equal(3, resolved.Id);
        Assert.Equal(1, accessor.EnumerationCount);
        Assert.Equal(1, accessor.UniversalNameLookupCount);
    }
}
