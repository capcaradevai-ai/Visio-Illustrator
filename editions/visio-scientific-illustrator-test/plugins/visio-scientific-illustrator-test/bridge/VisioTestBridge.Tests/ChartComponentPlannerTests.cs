using System.Text.Json;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Components;

namespace VisioTestBridge.Tests;

public sealed class ChartComponentPlannerTests
{
    [Theory]
    [InlineData("bar")]
    [InlineData("line")]
    [InlineData("scatter")]
    [InlineData("error_bar")]
    public void Plans_editable_axes_ticks_series_and_legend(string chartType)
    {
        var contract = Create(chartType);

        var plan = ChartComponentPlanner.CreatePlan(contract);

        Assert.Contains(plan.Primitives, item => item.SemanticId == "accuracy.axis.x");
        Assert.Contains(plan.Primitives, item => item.SemanticId == "accuracy.axis.y");
        Assert.Contains(plan.Primitives, item => item.SemanticId == "accuracy.axis.x.title");
        Assert.Contains(plan.Primitives, item => item.SemanticId == "accuracy.axis.y.title");
        Assert.Contains(plan.Primitives, item => item.SemanticId == "accuracy.legend.method-a.text");
        Assert.All(plan.Primitives, item =>
            Assert.True(item.Parameters.GetProperty("editable").GetBoolean()));
    }

    [Fact]
    public void Rejects_non_finite_mismatched_or_empty_series()
    {
        var mismatch = Create("line") with
        {
            Series = [new ChartSeries("a", [0, 1], [2], null, "#112233")],
        };
        AssertInvalid(mismatch);

        var nonFinite = Create("scatter") with
        {
            Series = [new ChartSeries("a", [0], [double.NaN], null, "#112233")],
        };
        AssertInvalid(nonFinite);

        AssertInvalid(Create("bar") with { Series = [] });
    }

    [Fact]
    public void Constant_domain_expands_by_a_deterministic_five_percent()
    {
        var contract = Create("line") with
        {
            Series = [new ChartSeries("a", [5, 5], [10, 10], null, "#112233")],
            XAxis = new ChartAxis("X", null, null, 5),
            YAxis = new ChartAxis("Y", null, null, 5),
        };

        var first = ChartComponentPlanner.CreatePlan(contract);
        var second = ChartComponentPlanner.CreatePlan(contract);

        Assert.Equal(
            first.Primitives.Select(item => item.Parameters.GetRawText()),
            second.Primitives.Select(item => item.Parameters.GetRawText()));
        Assert.Contains(first.Primitives, item =>
            item.Parameters.TryGetProperty("domain_min", out var minimum)
            && minimum.GetDouble() == 4.75);
    }

    private static ChartComponentContract Create(string chartType) =>
        new(
            new CompositeComponentContract(
                "accuracy",
                "chart",
                "results",
                new PixelBox(20, 20, 400, 260),
                JsonSerializer.SerializeToElement(new { background = "#FFFFFF" }),
                0),
            chartType,
            [new ChartSeries("method-a", [0, 1, 2], [0.4, 0.7, 0.9],
                chartType == "error_bar" ? [0.05, 0.04, 0.03] : null, "#2F75B5")],
            new ChartAxis("Epoch", 0, 2, 5),
            new ChartAxis("Accuracy", 0, 1, 5),
            new ChartLegend(true, "right"),
            "Accuracy");

    private static void AssertInvalid(ChartComponentContract contract)
    {
        var error = Assert.Throws<BridgeCommandException>(() =>
            ChartComponentPlanner.CreatePlan(contract));
        Assert.Equal(BridgeErrorCodes.ComponentContractInvalid, error.Code);
    }
}
