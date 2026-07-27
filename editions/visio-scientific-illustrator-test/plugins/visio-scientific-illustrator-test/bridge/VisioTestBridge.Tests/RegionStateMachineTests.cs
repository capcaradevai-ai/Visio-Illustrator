using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Quality;

namespace VisioTestBridge.Tests;

public sealed class RegionStateMachineTests
{
    [Theory]
    [InlineData(RegionQualityState.Planned, RegionQualityEvent.BeginDrawing, RegionQualityState.Drawing)]
    [InlineData(RegionQualityState.Drawing, RegionQualityEvent.FinishDrawing, RegionQualityState.AwaitingAudit)]
    [InlineData(RegionQualityState.AwaitingAudit, RegionQualityEvent.AuditFailed, RegionQualityState.Failed)]
    [InlineData(RegionQualityState.Failed, RegionQualityEvent.BeginCorrection, RegionQualityState.Correcting)]
    [InlineData(RegionQualityState.Correcting, RegionQualityEvent.FinishCorrection, RegionQualityState.AwaitingAudit)]
    [InlineData(RegionQualityState.AwaitingAudit, RegionQualityEvent.AuditPassed, RegionQualityState.Passed)]
    [InlineData(RegionQualityState.Passed, RegionQualityEvent.PageMutated, RegionQualityState.AwaitingAudit)]
    public void Allows_only_declared_transitions(
        RegionQualityState current,
        RegionQualityEvent qualityEvent,
        RegionQualityState expected)
    {
        Assert.Equal(expected, RegionStateMachine.Transition(current, qualityEvent));
    }

    [Theory]
    [InlineData(RegionQualityState.Drawing, RegionQualityEvent.AuditPassed)]
    [InlineData(RegionQualityState.Failed, RegionQualityEvent.AuditPassed)]
    [InlineData(RegionQualityState.Passed, RegionQualityEvent.BeginDrawing)]
    public void Rejects_invalid_transitions(
        RegionQualityState current,
        RegionQualityEvent qualityEvent)
    {
        var error = Assert.Throws<BridgeCommandException>(
            () => RegionStateMachine.Transition(current, qualityEvent));

        Assert.Equal(BridgeErrorCodes.AuditStateCorrupt, error.Code);
    }

    [Fact]
    public void Rejects_pass_when_open_hard_findings_exist()
    {
        var error = Assert.Throws<BridgeCommandException>(() =>
            RegionStateMachine.Transition(
                RegionQualityState.AwaitingAudit,
                RegionQualityEvent.AuditPassed,
                hasOpenHardFindings: true));

        Assert.Equal(BridgeErrorCodes.QualityGateBlocked, error.Code);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("accepted visual deviation", false)]
    public void Warning_waiver_requires_reason_and_current_evidence(
        string? reason,
        bool evidenceCurrent)
    {
        var error = Assert.Throws<BridgeCommandException>(() =>
            RegionStateMachine.Transition(
                RegionQualityState.AwaitingAudit,
                RegionQualityEvent.AuditPassed,
                warningWaiverRequested: true,
                warningWaiverReason: reason,
                warningEvidenceCurrent: evidenceCurrent));

        Assert.Equal(BridgeErrorCodes.AuditStateCorrupt, error.Code);
    }
}
