using System.Text.RegularExpressions;
using System.Text.Json;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Capabilities;
using VisioTestBridge.Core.Commands;
using VisioTestBridge.Core.Session;

namespace VisioTestBridge.Tests;

public sealed class CapabilityServiceTests
{
    [Fact]
    public void Summary_has_stable_identity_phase_and_profile_hash()
    {
        var service = new CapabilityService();
        var snapshot = CreateSnapshot();

        var first = service.GetCapabilities(snapshot);
        var second = service.GetCapabilities(snapshot);

        Assert.Equal("test", first.Edition);
        Assert.Equal("0.4.0-test.1", first.ProtocolVersion);
        Assert.Equal("installed", first.Phase);
        Assert.True(first.Visio.Installed);
        Assert.Null(first.Visio.Version);
        Assert.Null(first.Visio.Build);
        Assert.Equal("x64", first.Visio.Architecture);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), first.ProfileSha256);
        Assert.Equal(first.ProfileSha256, second.ProfileSha256);
        Assert.Equal(2, first.Families["masters"].Count);
    }

    [Fact]
    public void Profile_hash_is_independent_of_dictionary_insertion_order()
    {
        var service = new CapabilityService();
        var normal = CreateSnapshot();
        var reversed = normal with
        {
            Families = normal.Families
                .Reverse()
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        };

        Assert.Equal(
            service.GetCapabilities(normal).ProfileSha256,
            service.GetCapabilities(reversed).ProfileSha256);
    }

    [Fact]
    public void Details_are_paginated_without_returning_the_whole_family()
    {
        var service = new CapabilityService();

        var result = service.GetCapabilityDetails(
            CreateSnapshot(),
            new GetCapabilityDetailsCommand("masters", null, 1));

        Assert.Equal("masters", result.Category);
        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("1", result.NextCursor);
        Assert.Equal(0, result.Offset);
    }

    [Fact]
    public void Unknown_category_returns_capability_not_available()
    {
        var service = new CapabilityService();

        var error = Assert.Throws<BridgeCommandException>(() =>
            service.GetCapabilityDetails(
                CreateSnapshot(),
                new GetCapabilityDetailsCommand("unknown", null, 50)));

        Assert.Equal(BridgeErrorCodes.CapabilityNotAvailable, error.Code);
    }

    [Fact]
    public void Unavailable_entries_keep_their_explicit_reason()
    {
        var service = new CapabilityService();

        var result = service.GetCapabilityDetails(
            CreateSnapshot(),
            new GetCapabilityDetailsCommand("images", null, 50));

        var crop = Assert.Single(result.Items);
        Assert.False(crop.Available);
        Assert.Equal("unavailable", crop.Phase);
        Assert.Equal("No active Visio session.", crop.Reason);
    }

    [Fact]
    public void Installed_capability_query_routes_without_launching_Visio()
    {
        using var session = new VisioSession(Directory.GetCurrentDirectory());
        var router = new CommandRouter(session);
        using var summaryParameters = JsonDocument.Parse("{}");
        using var detailParameters = JsonDocument.Parse(
            """{"category":"masters","limit":1}""");

        var summary = Assert.IsType<CapabilitySummary>(
            router.Route("get_capabilities", summaryParameters.RootElement));
        var details = Assert.IsType<CapabilityDetailsResult>(
            router.Route("get_capability_details", detailParameters.RootElement));
        var json = JsonSerializer.Serialize(summary, ProtocolJson.Options);

        Assert.Equal("installed", summary.Phase);
        Assert.Equal("masters", details.Category);
        Assert.Single(details.Items);
        Assert.Contains("\"protocol_version\":\"0.4.0-test.1\"", json);
        Assert.Contains("\"profile_sha256\":\"", json);
        Assert.Equal(SessionState.Stopped, session.State);
        Assert.Null(session.OwnedProcessId);
    }

    private static CapabilityProbeSnapshot CreateSnapshot()
    {
        var families = new Dictionary<string, IReadOnlyList<CapabilityEntry>>(
            StringComparer.Ordinal)
        {
            ["masters"] =
            [
                new("rectangle", "Rectangle", true, "installed"),
                new("router", "Router", true, "installed"),
            ],
            ["images"] =
            [
                new(
                    "crop",
                    "Image crop",
                    false,
                    "unavailable",
                    "No active Visio session."),
            ],
            ["limitations"] = [],
        };
        return new CapabilityProbeSnapshot(
            "installed",
            new CapabilityVisioInfo(true, null, null, "x64"),
            families,
            [new CapabilityLimitation("SESSION_REQUIRED", "Session details require a live Visio window.")]);
    }
}
