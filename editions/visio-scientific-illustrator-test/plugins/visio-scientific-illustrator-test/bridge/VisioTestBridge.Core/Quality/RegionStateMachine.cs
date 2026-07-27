using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality;

public static class RegionStateMachine
{
    private static readonly IReadOnlyDictionary<
        (RegionQualityState State, RegionQualityEvent Event),
        RegionQualityState> Transitions =
        new Dictionary<
            (RegionQualityState, RegionQualityEvent),
            RegionQualityState>
        {
            [(RegionQualityState.Planned, RegionQualityEvent.BeginDrawing)] =
                RegionQualityState.Drawing,
            [(RegionQualityState.Drawing, RegionQualityEvent.FinishDrawing)] =
                RegionQualityState.AwaitingAudit,
            [(RegionQualityState.AwaitingAudit, RegionQualityEvent.AuditFailed)] =
                RegionQualityState.Failed,
            [(RegionQualityState.Failed, RegionQualityEvent.BeginCorrection)] =
                RegionQualityState.Correcting,
            [(RegionQualityState.Correcting, RegionQualityEvent.FinishCorrection)] =
                RegionQualityState.AwaitingAudit,
            [(RegionQualityState.AwaitingAudit, RegionQualityEvent.AuditPassed)] =
                RegionQualityState.Passed,
            [(RegionQualityState.Passed, RegionQualityEvent.PageMutated)] =
                RegionQualityState.AwaitingAudit,
        };

    public static RegionQualityState Transition(
        RegionQualityState current,
        RegionQualityEvent qualityEvent,
        bool hasOpenHardFindings = false,
        bool warningWaiverRequested = false,
        string? warningWaiverReason = null,
        bool warningEvidenceCurrent = true)
    {
        if (
            qualityEvent == RegionQualityEvent.AuditPassed &&
            hasOpenHardFindings
        )
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.QualityGateBlocked,
                "存在未解决的 hard finding，区域不能通过。");
        }

        if (
            qualityEvent == RegionQualityEvent.AuditPassed &&
            warningWaiverRequested &&
            (string.IsNullOrWhiteSpace(warningWaiverReason) ||
             !warningEvidenceCurrent)
        )
        {
            throw Corrupt(
                "warning waiver 必须包含原因并绑定当前审查证据。");
        }

        if (!Transitions.TryGetValue((current, qualityEvent), out var next))
        {
            throw Corrupt(
                $"不允许从 {current} 通过 {qualityEvent} 转换状态。");
        }
        return next;
    }

    private static BridgeCommandException Corrupt(string message) =>
        new(BridgeErrorCodes.AuditStateCorrupt, message);
}
