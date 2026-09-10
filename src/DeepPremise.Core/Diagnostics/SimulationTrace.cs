using System.Runtime.CompilerServices;

namespace DeepPremise.Core;

// Optional local developer diagnostics. Never part of a save or player-facing read model.
public sealed record SimulationDecision(long Tick, string System, string Decision, string Reason, object? Facts);

public interface IRecordedWorld
{
    string RecordingKind { get; }
    long Tick { get; }
    string SaveJson();
    Action<SimulationDecision>? DecisionRecorded { get; set; }
    Action? TickCompleted { get; set; }
}

public sealed partial class SimulationRunner : IRecordedWorld
{
    public string RecordingKind => "conversation-v3";
    public Action<SimulationDecision>? DecisionRecorded { get; set; }
    public Action? TickCompleted { get; set; }
    private void Trace(string system, string decision, string reason, object? facts = null) =>
        DecisionRecorded?.Invoke(new SimulationDecision(state.Tick, system, decision, reason, facts));
}
