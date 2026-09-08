namespace DeepPremise.Core;

// Domain types never reference Godot. This graph is persistence-only, not UI data.
public sealed class WorldState
{
    public int Version { get; set; } = 3;
    public uint RandomState { get; set; } = 271828;
    public long Tick { get; set; } = 32;
    public int NextEvent { get; set; } = 1;
    public int NextLine { get; set; } = 1;
    public string PlayerPlace { get; set; } = "courtyard";
    public List<Agent> Agents { get; set; } = [];
    public List<WorldEvent> Events { get; set; } = [];
    public List<ConversationLine> Transcript { get; set; } = [];
    public List<PlayerAccount> Accounts { get; set; } = [];
    public List<JournalEntry> Journal { get; set; } = [];
    public List<Delivery> Deliveries { get; set; } = [];
    public List<Obligation> Obligations { get; set; } = [];
    public List<RecentQuestion> Questions { get; set; } = [];
    public string Notes { get; set; } = "";
    public int Bread { get; set; } = 8;
    public int SharedMeals { get; set; }
    public int CompletedDeliveries { get; set; }
}

public sealed class Agent
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Home { get; set; } = "";
    public string Place { get; set; } = "";
    public string Seat { get; set; } = "";
    public int Hunger { get; set; }
    public int Reserve { get; set; }
    public Dictionary<string, int> Ties { get; set; } = [];
    public List<Knowledge> Knowledge { get; set; } = [];
}

public sealed class Knowledge
{
    public int EventId { get; set; }
    public string Claim { get; set; } = "";
    public string Source { get; set; } = "";
    public bool Witnessed { get; set; }
    public int Strength { get; set; }
    public long LearnedAt { get; set; }
}

public sealed class WorldEvent
{
    public int Id { get; set; }
    public long Tick { get; set; }
    public string Kind { get; set; } = "";
    public string Place { get; set; } = "";
    public string Actor { get; set; } = "";
    public string Claim { get; set; } = "";
}

public sealed class Delivery
{
    public string Carrier { get; set; } = "iven";
    public string Destination { get; set; } = "workshop";
    public long Due { get; set; }
    public int Amount { get; set; }
    public string Witness { get; set; } = "";
}

public sealed class Obligation
{
    public string Seat { get; set; } = "east";
    public string Creditor { get; set; } = "mara";
    public int Amount { get; set; } = 2;
    public bool Acknowledged { get; set; }
}

public sealed record ConversationLine(int Id, long Tick, string Speaker, string Text, bool IsPlayerInput = false, Dictionary<string, string>? Voices = null);
public sealed record PlayerAccount(int EventId, string Claim, string Source, long HeardAt);
public sealed record JournalEntry(long Tick, string Source, string Text);
public sealed record RecentQuestion(string AgentId, string Topic, long Tick);

// Public read models are intentionally independent of the persistence graph.
public sealed record ResidentView(string Id, string Name, string Role, string Place, string Activity);
public sealed record PlaceView(string Id, string Name, string Description, float X, float Y);
public sealed record DialogueChoice(string Id, string Text, bool Enabled = true);
public sealed record WorldView(long Tick, string Time, string Place, string PlaceName, string Atmosphere,
    int Bread, IReadOnlyList<ResidentView> Residents, IReadOnlyList<PlaceView> Places,
    IReadOnlyList<ConversationLine> Transcript, IReadOnlyList<JournalEntry> Journal,
    IReadOnlyList<PlayerAccount> Accounts, string Notes);
public sealed record ActionResult(bool Success, string Message);
