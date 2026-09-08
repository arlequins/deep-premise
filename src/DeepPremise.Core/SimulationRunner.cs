namespace DeepPremise.Core;

public sealed partial class SimulationRunner
{
    private WorldState state;
    private static readonly PlaceView[] Places =
    [
        new("courtyard", "The Common Table", "Six cups. One empty chair. Someone has set a place for you.", .48f, .48f),
        new("bakery", "Mara's Oven", "The window is open. Warm air carries the smell of orange peel.", .22f, .32f),
        new("workshop", "The Mending Room", "A kettle ticks on a cold stove. Two coats hang from the same peg.", .73f, .29f),
        new("landing", "Reed Landing", "The water is close enough to hear beneath the boards.", .69f, .77f)
    ];
    private static readonly string[] AgentIds = ["mara", "iven", "sela", "orren", "neri", "tavi"];
    public long Tick => state.Tick;

    public SimulationRunner(uint seed = 271828)
    {
        state = new WorldState { RandomState = seed == 0 ? 1 : seed };
        state.Agents =
        [
            NewAgent("mara", "Mara", "Baker", "bakery", "oven"),
            NewAgent("iven", "Iven", "Carrier", "landing", "reed"),
            NewAgent("sela", "Sela", "Table keeper", "courtyard", "table"),
            NewAgent("orren", "Orren", "Mender", "workshop", "west"),
            NewAgent("neri", "Neri", "Letter keeper", "courtyard", "letters"),
            NewAgent("tavi", "Tavi", "New mender", "workshop", "east")
        ];
        foreach (var person in state.Agents) person.Reserve = 1 + Next(5);
        foreach (var person in state.Agents)
            foreach (var other in state.Agents.Where(a => a != person))
                person.Ties[other.Id] = 2 + Next(4);
        state.Obligations.Add(new Obligation());
        // Establish an actual event. Different people have different access to it.
        var parcelPlaces = new[] { ("landing", "beneath the landing bench"), ("workshop", "beside the cold stove"), ("bakery", "under the bakery window") };
        var parcelPlace = parcelPlaces[Next(parcelPlaces.Length)];
        var parcel = Record("parcel", parcelPlace.Item1, "iven", $"The blue parcel was left {parcelPlace.Item2}.", false);
        parcel.Tick = state.Tick - 4;
        Learn(Person("iven"), parcel, parcel.Claim, "Iven", true);
        Learn(Person("sela"), parcel, "Iven carried a blue parcel towards the mending room.", "Mara", false);
        Learn(Person("mara"), parcel, "A blue parcel was meant for the mending room.", "Iven", false);
        Journal("Neri's note", "Take the chair by the window. If someone tells you something curious, ask someone else before writing it down as fact.");
        Line("Neri", "You are early. Good. There is bread on the table, and nobody has decided whose morning it is yet.");
    }

    private SimulationRunner(WorldState saved) => state = saved;
    private static Agent NewAgent(string id, string name, string role, string home, string seat) =>
        new() { Id = id, Name = name, Role = role, Home = home, Place = home, Seat = seat, Reserve = 3 };
    private Agent Person(string id) => state.Agents.Single(a => a.Id == id);
    private int Next(int maximum)
    {
        var x = state.RandomState;
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        state.RandomState = x;
        return (int)(x % (uint)maximum);
    }
    public void Run(int ticks)
    {
        if (ticks is < 0 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(ticks));
        for (var i = 0; i < ticks; i++) Step();
    }
    private void Step()
    {
        state.Tick++;
        var time = (int)(state.Tick % 96);
        foreach (var person in state.Agents)
        {
            person.Place = time is >= 48 and < 60 or >= 76 and < 84 ? "courtyard" : person.Home;
            if (person.Id == "iven" && time is >= 36 and < 48) person.Place = "bakery";
            if (person.Id == "neri" && time is >= 36 and < 44) person.Place = "workshop";
            if (time is >= 60 and < 76)
            {
                // Afternoon visits arise from needs and ties; no omniscient storyteller selects them.
                if (person.Hunger >= 2) person.Place = "bakery";
                else if (person.Ties.Values.Max() >= 5)
                {
                    var friend = person.Ties.OrderByDescending(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal).First().Key;
                    person.Place = Person(friend).Home;
                }
            }
        }
        foreach (var delivery in state.Deliveries.Where(d => d.Due <= state.Tick).ToArray())
        {
            state.Deliveries.Remove(delivery);
            var carrier = Person(delivery.Carrier);
            carrier.Place = delivery.Destination;
            var recipient = state.Agents.First(a => a.Home == delivery.Destination && a.Id != carrier.Id);
            recipient.Reserve = Math.Min(20, recipient.Reserve + delivery.Amount);
            state.CompletedDeliveries++;
            var e = Record("delivery", delivery.Destination, carrier.Id,
                $"Iven left {delivery.Amount} loaves at {Places.Single(p => p.Id == delivery.Destination).Name}.");
            Learn(carrier, e, e.Claim, carrier.Name, true);
            if (delivery.Witness != "")
            {
                var witness = Person(delivery.Witness);
                Learn(witness, e, e.Claim, carrier.Name, true);
                Journal($"{witness.Name}'s receipt", $"I was there when the bread arrived. I kept one corner of the wrapping for you.");
            }
        }
        if (time == 32)
        {
            state.Bread = Math.Min(16, state.Bread + 4);
            foreach (var a in state.Agents) a.Reserve = Math.Min(8, a.Reserve + 1);
            Record("baking", "bakery", "mara", "Mara left four warm loaves for the common table.");
        }
        if (time is 48 or 76) Eat();
        if (state.Tick % 4 == 0) Exchange();
        if (state.Tick % 12 == 0 && time is >= 28 and < 84) Encounter();
        if (state.Tick % 24 == 0) Forget();
        if (time == 64)
        {
            var a = Person("tavi");
            if (state.Obligations.Any(o => o.Seat == a.Seat && !o.Acknowledged))
                Record("account", a.Home, a.Id, "Tavi received another bill addressed to the east chair.");
        }
        Trim();
    }
    private void Eat()
    {
        foreach (var a in state.Agents)
        {
            if (a.Reserve > 0) { a.Reserve--; a.Hunger = Math.Max(0, a.Hunger - 1); }
            else a.Hunger = Math.Min(8, a.Hunger + 1);
        }
        var hungry = state.Agents.Where(a => a.Hunger > 0).OrderByDescending(a => a.Hunger).FirstOrDefault();
        var giver = state.Agents.Where(a => a.Reserve > 2).OrderByDescending(a => a.Reserve).FirstOrDefault();
        if (hungry != null && giver != null)
        {
            giver.Reserve--; hungry.Reserve++; hungry.Ties[giver.Id] = Math.Min(12, hungry.Ties[giver.Id] + 1);
            Record("meal", "courtyard", giver.Id, $"{giver.Name} put an extra bowl beside {hungry.Name}.");
        }
        else if (Next(3) == 0)
            Record("ordinary", "courtyard", "sela", "Sela told the old joke about the rain. Orren laughed before the ending.");
    }
    private void Exchange()
    {
        var a = state.Agents[Next(state.Agents.Count)];
        var others = state.Agents.Where(b => b.Id != a.Id && b.Place == a.Place).ToArray();
        if (others.Length == 0 || a.Knowledge.Count == 0) return;
        var b = others[Next(others.Length)];
        var knowledge = a.Knowledge[Next(a.Knowledge.Count)];
        if (knowledge.Strength < 2) return;
        var sourceEvent = state.Events.FirstOrDefault(e => e.Id == knowledge.EventId);
        if (sourceEvent == null) return;
        var claim = knowledge.Claim;
        if (!knowledge.Witnessed && Next(5) == 0 && sourceEvent.Kind == "parcel")
            claim = "Someone said the blue parcel had already reached the mending room.";
        var existing = b.Knowledge.FirstOrDefault(k => k.EventId == knowledge.EventId);
        if (existing is { Witnessed: true })
        {
            if (existing.Claim == claim) existing.Strength = Math.Min(12, existing.Strength + 1);
            return;
        }
        if (existing == null || b.Ties[a.Id] >= 4)
            Learn(b, sourceEvent, claim, a.Name, false);
    }
    private void Encounter()
    {
        var a = state.Agents[Next(state.Agents.Count)];
        var neighbors = state.Agents.Where(b => b.Id != a.Id && b.Place == a.Place).ToArray();
        if (neighbors.Length == 0) return;
        var b = neighbors[Next(neighbors.Length)];
        if (b.Hunger > 0 && a.Reserve > 1)
        {
            a.Reserve--; b.Reserve++; b.Hunger--;
            b.Ties[a.Id] = Math.Min(12, b.Ties[a.Id] + 2);
            Record("gift", a.Place, a.Id, $"{a.Name} gave {b.Name} the last piece of a loaf without asking for a name on the receipt.");
        }
        else
        {
            var disputed = a.Knowledge.FirstOrDefault(k => b.Knowledge.Any(other => other.EventId == k.EventId && other.Claim != k.Claim && other.Strength >= 4));
            if (disputed != null && a.Ties[b.Id] <= 5)
            {
                a.Ties[b.Id] = Math.Max(0, a.Ties[b.Id] - 1);
                Record("dispute", a.Place, a.Id, $"{a.Name} stopped speaking when {b.Name} supplied a different ending to the same story.");
            }
            else if (a.Ties[b.Id] >= 4)
            {
                a.Ties[b.Id] = Math.Min(12, a.Ties[b.Id] + 1);
                var smallActs = new[] { "saved a warm cup for", "moved a chair closer to", "returned a neatly folded coat to", "left the last orange for" };
                Record("kindness", a.Place, a.Id, $"{a.Name} {smallActs[Next(smallActs.Length)]} {b.Name}.");
            }
        }
    }
    private void Forget()
    {
        foreach (var person in state.Agents)
        {
            foreach (var knowledge in person.Knowledge)
            {
                // Recollection persists through a living, shared context, not elapsed time alone.
                var anchored = state.Agents.Any(other => other.Id != person.Id && other.Place == person.Place &&
                    other.Knowledge.Any(k => k.EventId == knowledge.EventId && k.Claim == knowledge.Claim && k.Strength >= 3));
                knowledge.Strength = Math.Clamp(knowledge.Strength + (anchored ? 1 : -2), 0, 12);
            }
            person.Knowledge.RemoveAll(k => k.Strength == 0);
        }
    }
    private WorldEvent Record(string kind, string place, string actor, string claim, bool witnessed = true)
    {
        var e = new WorldEvent { Id = state.NextEvent++, Tick = state.Tick, Kind = kind, Place = place, Actor = actor, Claim = claim };
        state.Events.Add(e);
        if (witnessed)
        {
            foreach (var a in state.Agents.Where(a => a.Place == place)) Learn(a, e, claim, a.Name, true);
            if (state.PlayerPlace == place) Journal("What you saw", claim);
        }
        return e;
    }
    private void Learn(Agent a, WorldEvent e, string claim, string source, bool witnessed)
    {
        var old = a.Knowledge.FirstOrDefault(k => k.EventId == e.Id);
        if (old is { Witnessed: true } && !witnessed) return;
        if (old != null) a.Knowledge.Remove(old);
        a.Knowledge.Add(new Knowledge { EventId = e.Id, Claim = claim, Source = source, Witnessed = witnessed, Strength = witnessed ? 8 : 5, LearnedAt = state.Tick });
        if (a.Knowledge.Count > 32) a.Knowledge.RemoveAt(0);
    }
    private void Line(string speaker, string text, bool isPlayerInput = false) => state.Transcript.Add(new ConversationLine(state.NextLine++, state.Tick, speaker, text, isPlayerInput));
    private void Journal(string source, string text) => state.Journal.Add(new JournalEntry(state.Tick, source, text));
    private void Trim()
    {
        KeepLast(state.Transcript, 160); KeepLast(state.Journal, 100); KeepLast(state.Events, 512);
        KeepLast(state.Accounts, 48); KeepLast(state.Questions, 64);
    }
    private static void KeepLast<T>(List<T> list, int limit)
    { if (list.Count > limit) list.RemoveRange(0, list.Count - limit); }
    public ActionResult Travel(string place)
    {
        if (!Places.Any(p => p.Id == place)) return new(false, "That place is not nearby.");
        if (state.PlayerPlace == place) return new(true, "You are already here.");
        state.PlayerPlace = place;
        Run(1);
        Journal("Your walk", Places.Single(p => p.Id == place).Description);
        Trim();
        return new(true, "You take the short walk.");
    }
    public ActionResult SetNotes(string text)
    {
        if (text == null || text.Length > 5000) return new(false, "The notebook holds 5,000 characters.");
        state.Notes = text;
        return new(true, "Notebook saved.");
    }
    public WorldView Observe()
    {
        var place = Places.Single(p => p.Id == state.PlayerPlace);
        var hour = state.Tick % 96 / 4;
        var atmosphere = hour < 6 || hour >= 21 ? "Lights linger behind the shutters. The paths are quiet." :
            hour is >= 12 and < 15 ? "Chairs scrape the courtyard stones. It is time to eat together." : place.Description;
        return new WorldView(state.Tick, FormatTime(state.Tick), place.Id, place.Name, atmosphere,
            state.Bread, state.Agents.Select(a => new ResidentView(a.Id, a.Name, a.Role, a.Place,
                a.Hunger >= 3 ? "Keeps glancing at the bread." : a.Place == "courtyard" ? "Has pulled up a chair." : "Getting on with the day.")).ToArray(),
            Array.AsReadOnly(Places), state.Transcript.Select(l => l with { Voices = l.Voices == null ? null : new Dictionary<string, string>(l.Voices) }).ToArray(), state.Journal.AsEnumerable().Reverse().ToArray(),
            state.Accounts.ToArray(), state.Notes);
    }
    public static string FormatTime(long tick) => $"Day {tick / 96 + 1}  /  {tick % 96 / 4:00}:{tick % 4 * 15:00}";
}
