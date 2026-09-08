using System.Runtime.CompilerServices;

namespace DeepPremise.Core;

// Optional local developer diagnostics. Never part of a save or player-facing read model.
public sealed record SimulationDecision(long Tick, string System, string Decision, string Reason, object? Facts);

public sealed partial class SimulationRunner
{
    public Action<SimulationDecision>? DecisionRecorded { get; set; }
    public Action? TickCompleted { get; set; }
    private void Trace(string system, string decision, string reason, object? facts = null) =>
        DecisionRecorded?.Invoke(new SimulationDecision(state.Tick, system, decision, reason, facts));
}
