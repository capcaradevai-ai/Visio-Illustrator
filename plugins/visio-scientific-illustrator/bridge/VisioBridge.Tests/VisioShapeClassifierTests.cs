using VisioBridge.Core.Shapes;

namespace VisioBridge.Tests;

public sealed class VisioShapeClassifierTests
{
    [Fact]
    public void IsConnector_returns_false_for_2D_shape()
    {
        Assert.False(VisioShapeClassifier.IsConnector(0, false));
    }

    [Fact]
    public void IsConnector_returns_true_for_1D_shape()
    {
        Assert.True(VisioShapeClassifier.IsConnector(1, false));
    }

    [Fact]
    public void IsConnector_returns_false_for_1D_group()
    {
        Assert.False(VisioShapeClassifier.IsConnector(1, true));
    }

    [Fact]
    public void IsOneDimensionalGroup_identifies_1D_group()
    {
        Assert.True(VisioShapeClassifier.IsOneDimensionalGroup(1, true));
        Assert.False(VisioShapeClassifier.IsOneDimensionalGroup(1, false));
        Assert.False(VisioShapeClassifier.IsOneDimensionalGroup(0, true));
    }
}
