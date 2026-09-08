namespace DeepPremise.Core;

public sealed class NeighborhoodStories
{
    public int Revision { get; set; }
    public int NextId { get; set; } = 1;
    public List<NeighborhoodRequest> Requests { get; set; } = [];
    public Dictionary<string, int> Familiarity { get; set; } = [];
    public Dictionary<string, long> LastConversationDay { get; set; } = [];
    public int SharedSuppers { get; set; }
    public int Repairs { get; set; }
    public int DeliveredLetters { get; set; }
    public List<UnfinishedConversation> Conversations { get; set; } = [];
    public Dictionary<string, string> AfternoonVisits { get; set; } = [];
}

public sealed class NeighborhoodRequest
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string Requester { get; set; } = "";
    public string Target { get; set; } = "";
    public string Place { get; set; } = "";
    public string Status { get; set; } = "offered";
    public long Created { get; set; }
    public long ReadyAt { get; set; }
    public long ResolvedAt { get; set; }
    public bool Known { get; set; }
    public bool Contributed { get; set; }
    public bool Invited { get; set; }
    public bool Found { get; set; }
    public int Variant { get; set; }
}

public sealed record ConversationThreadView(int Id, string Title, string Requester, string Status, string NextStep);

public sealed partial class SimulationRunner
{
    private static void ValidateStories(WorldState state)
    {
        void Require(bool condition) { if (!condition) throw new InvalidDataException("Invalid neighborhood stories."); }
        var stories = state.Stories;
        Require(stories != null);
        Require(stories!.Revision is 0 or 1 && stories.NextId > 0);
        Require(stories.Requests.Count <= 31 && stories.SharedSuppers >= 0 && stories.Repairs >= 0 && stories.DeliveredLetters >= 0);
        if (stories.Revision == 0)
        {
            Require(stories.Requests.Count == 0 && stories.Familiarity.Count == 0 && stories.LastConversationDay.Count == 0);
            return;
        }
        Require(stories.AfternoonVisits.Count is 0 or 6 && stories.AfternoonVisits.All(p => AgentIds.Contains(p.Key) && AgentIds.Contains(p.Value) && p.Key != p.Value));
        Require(stories.Conversations.Count <= 12);
        Require(stories.Conversations.Select(c => (c.AgentId, c.Scene)).Distinct().Count() == stories.Conversations.Count);
        foreach (var c in stories.Conversations)
        {
            Require(AgentIds.Contains(c.AgentId) && c.Scene is 0 or 1 && c.OpenedAt >= 0 && c.OpenedAt <= state.Tick);
            Require(c.Answer is "" or "listen" or "press" && c.AnsweredAt >= 0 && c.AnsweredAt <= state.Tick);
            Require(!c.Revisited || c.Answer != "");
            Require(c.Outcome.Length <= 1200 && c.OutcomeAt >= 0 && c.OutcomeAt <= state.Tick);
        }
        Require(stories.Familiarity.Count == 6 && stories.LastConversationDay.Count == 6);
        Require(AgentIds.All(id => stories.Familiarity.ContainsKey(id) && stories.LastConversationDay.ContainsKey(id)));
        Require(stories.Familiarity.Values.All(v => v is >= 0 and <= 12));
        Require(stories.LastConversationDay.Values.All(v => v >= -1 && v <= state.Tick / 96));
        Require(stories.Requests.Select(r => r.Id).Distinct().Count() == stories.Requests.Count);
        foreach (var r in stories.Requests)
        {
            Require(r.Id > 0 && r.Id < stories.NextId && RequestKinds.Contains(r.Kind));
            Require(AgentIds.Contains(r.Requester) && AgentIds.Contains(r.Target));
            Require(r.Requester != r.Target || r.Kind == "bread");
            Require(Places.Any(p => p.Id == r.Place) && r.Variant is >= 0 and <= 2);
            Require(r.Status is "offered" or "accepted" or "waiting" or "ready" or "complete" or "settled" or "declined");
            Require(r.Created >= 0 && r.Created <= state.Tick && r.ResolvedAt >= 0 && r.ResolvedAt <= state.Tick);
            Require(r.ReadyAt >= 0 && r.ReadyAt <= state.Tick + 96);
            Require(r.Status != "waiting" || r.Kind is "repair" or "supper");
        }
    }
    private static readonly string[] RequestKinds = ["bread", "repair", "letter", "search", "supper", "accounts"];
    private void InitializeStories()
    {
        if (state.Stories.Revision != 0) return;
        state.Stories.Revision = 1;
        foreach (var id in AgentIds) { state.Stories.Familiarity[id] = 0; state.Stories.LastConversationDay[id] = -1; }
        AddRequest("repair", "orren", "tavi", "workshop");
        AddRequest("supper", "sela", "tavi", "courtyard");
        AddRequest("search", "mara", "iven", "landing");
        AddRequest("letter", "neri", "orren", "workshop");
        PlanVisits();
    }
    private void AddRequest(string kind, string requester, string target, string place)
    {
        if (state.Stories.Requests.Count(r => r.Status is "offered" or "accepted" or "waiting" or "ready") >= 7) return;
        if (state.Stories.Requests.Any(r => r.Kind == kind && r.Status is "offered" or "accepted" or "waiting" or "ready")) return;
        Trace("request", "created", "Daily needs and social ties selected an available request pattern.", new { kind, requester, target, place });
        state.Stories.Requests.Add(new NeighborhoodRequest { Id = state.Stories.NextId++, Kind = kind, Requester = requester,
            Target = target, Place = place, Created = state.Tick, Variant = Next(3) });
    }
    private void AdvanceStories()
    {
        foreach (var request in state.Stories.Requests.ToArray())
        {
            if (request.Status == "waiting" && request.ReadyAt <= state.Tick)
            {
                request.Status = "ready";
                Trace("request", "ready", "The scheduled work or supper has reached its time.", new { request.Id, request.Kind, request.ReadyAt });
                if (request.Kind == "supper")
                {
                    state.Stories.SharedSuppers++;
                    Person(request.Target).Ties[request.Requester] = Math.Min(12, Person(request.Target).Ties[request.Requester] + 2);
                    foreach (var person in state.Agents) person.Hunger = Math.Max(0, person.Hunger - 1);
                    Record("supper", "courtyard", request.Requester,
                        $"{Person(request.Target).Name} stayed after supper to help {Person(request.Requester).Name} wash the cups.");
                }
                if (request.Known) Journal(Person(request.Requester).Name + "'s note", "Come by when you have a moment. There is something to show you.");
            }
            if (request.Status is "offered" or "accepted" && state.Tick - request.Created > 384)
            {
                request.Status = "settled"; request.ResolvedAt = state.Tick;
                Trace("request", "settled-without-player", "Four days have passed; neighbors find another pair of hands.", new { request.Id, request.Kind, request.Created });
                if (request.Target != request.Requester)
                    Person(request.Target).Ties[request.Requester] = Math.Min(12, Person(request.Target).Ties[request.Requester] + 1);
                if (request.Known) Journal(Person(request.Requester).Name + "'s note", "We found another pair of hands. Come over anyway; the conversation can wait for you.");
            }
        }
        if (state.Tick % 96 == 32)
        {
            var hungry = state.Agents.OrderBy(a => a.Reserve).ThenBy(a => a.Id, StringComparer.Ordinal).First();
            if (hungry.Reserve <= 3) AddRequest("bread", hungry.Id, "mara", hungry.Home);
            var day = state.Tick / 96;
            if (day % 3 == 1) AddRequest("repair", "orren", "tavi", "workshop");
            if (day % 3 == 2) AddRequest("supper", "sela", state.Agents.Where(a => a.Id != "sela").ToArray()[Next(5)].Id, "courtyard");
            var person = state.Agents[Next(6)];
            var target = person.Ties.OrderBy(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal).First().Key;
            AddRequest(person.Ties[target] <= 3 ? "accounts" : "letter", person.Id, target, Person(target).Home);
            if (day % 2 == 0) AddRequest("search", person.Id, target, Places[Next(4)].Id);
        }
        var old = state.Stories.Requests.Where(r => r.Status is "complete" or "settled" or "declined").OrderByDescending(r => r.ResolvedAt).Skip(24).ToArray();
        foreach (var r in old) state.Stories.Requests.Remove(r);
    }
    private void PlanVisits()
    {
        foreach (var a in state.Agents)
        {
            var candidates = a.Ties.OrderBy(t => t.Key, StringComparer.Ordinal).ToArray();
            var draw = Next(candidates.Sum(t => t.Value + 1));
            var chosen = candidates[^1].Key;
            foreach (var candidate in candidates)
            {
                draw -= candidate.Value + 1;
                if (draw < 0) { chosen = candidate.Key; break; }
            }
            state.Stories.AfternoonVisits[a.Id] = chosen;
            Trace("routine", "plan-visit", "Familiar people are more likely, but weaker ties still receive visits. The plan lasts for this day.",
                new { a.Id, Target = chosen, Candidates = candidates });
        }
    }
    private void RegisterVisit(string id)
    {
        var day = state.Tick / 96;
        if (state.Stories.LastConversationDay[id] == day) return;
        state.Stories.LastConversationDay[id] = day;
        state.Stories.Familiarity[id] = Math.Min(12, state.Stories.Familiarity[id] + 1);
    }
    private void AddStoryChoices(Agent a, List<DialogueChoice> choices)
    {
        choices.Add(new("help", "Is there anything you need a hand with?"));
        if (state.Stories.Familiarity[a.Id] >= 2) choices.Add(new("personal", "What keeps you in this neighborhood?"));
        var neighbor = a.Ties.OrderByDescending(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal).First().Key;
        choices.Add(new("neighbor", $"How do you get along with {Person(neighbor).Name}?"));
        foreach (var r in state.Stories.Requests.Where(r => r.Known))
        {
            var firstChoice = choices.Count;
            string Action(string suffix) => $"request:{r.Id}:{suffix}";
            if (r.Requester == a.Id && r.Status == "offered")
            {
                choices.Add(new(Action("accept"), "I can help with that."));
                choices.Add(new(Action("decline"), "I cannot promise that today."));
            }
            if (r.Status == "accepted")
            {
                if (r.Requester == a.Id)
                {
                    if (r.Kind == "bread") choices.Add(new(Action("finish"), "Give two loaves from the common table.", state.Bread >= 2));
                    if (r.Kind == "repair") choices.Add(new(Action("work"), "Hold the hinge while they mend it."));
                    if (r.Kind == "search" && r.Found) choices.Add(new(Action("finish"), "Return the small thing you found."));
                    if (r.Kind == "accounts" && r.Found)
                    {
                        choices.Add(new(Action("finish"), "Keep both accounts, with both names attached."));
                        choices.Add(new(Action("side"), "Say their version should be the one repeated."));
                    }
                    if (r.Kind == "supper" && !r.Contributed) choices.Add(new(Action("food"), "Bring two loaves for the evening table.", state.Bread >= 2));
                }
                if (r.Target == a.Id)
                {
                    if (r.Kind == "letter")
                    {
                        choices.Add(new(Action("finish"), $"Give {a.Name} the folded letter, still closed."));
                        choices.Add(new(Action("paraphrase"), "Explain the letter in your own words instead."));
                    }
                    if (r.Kind == "supper" && !r.Invited) choices.Add(new(Action("invite"), "Invite them to stay for the evening meal."));
                    if (r.Kind == "accounts" && !r.Found) choices.Add(new(Action("hear"), "Ask for their account before repeating anything."));
                }
            }
            if (r.Requester == a.Id && r.Status == "ready") choices.Add(new(Action("finish"), "You said there was something to show me."));
            for (var i = firstChoice; i < choices.Count; i++) choices[i] = choices[i] with { Text = RequestTitle(r) + " · " + choices[i].Text };
        }
    }
    private string Help(Agent a)
    {
        var request = state.Stories.Requests.FirstOrDefault(r => r.Requester == a.Id && r.Status == "offered");
        if (request == null)
        {
            var existing = state.Stories.Requests.FirstOrDefault(r => r.Requester == a.Id && r.Known && r.Status is "accepted" or "waiting" or "ready");
            return existing == null ? "Nothing urgent. You can sit here without earning the chair, you know." : RequestNext(existing);
        }
        request.Known = true;
        return request.Kind switch
        {
            "bread" => "I have been putting off lunch to finish this. Could you spare two loaves? One is for later. I keep forgetting that later arrives.",
            "repair" => "This hinge needs another pair of hands. I can do the careful part if you keep it still. Come back afterwards; it takes time for a repair to settle.",
            "letter" => $"Would you take a folded letter to {Person(request.Target).Name}? Leave it closed. The space before the first word belongs to them.",
            "search" => $"I left a small keepsake at {Places.Single(p => p.Id == request.Place).Name}. Look around there if you pass by. I would rather describe it after you find it.",
            "supper" => $"We have room for {Person(request.Target).Name} this evening. Would you ask them, and bring two loaves? The food and the invitation need not come from the same hand.",
            _ => $"{Person(request.Target).Name} and I remember the same afternoon differently. Will you hear them before you decide what I meant?"
        };
    }
    private string ResolveRequest(Agent a, string topic)
    {
        var parts = topic.Split(':');
        var r = state.Stories.Requests.Single(r => r.Id.ToString() == parts[1]);
        Trace("request", "player-response", "A currently available request action was selected.", new { r.Id, r.Kind, r.Status, Action = parts[2], Resident = a.Id });
        switch (parts[2])
        {
            case "accept": r.Status = "accepted"; return "Thank you. No need to hurry past everyone else on my account.";
            case "decline": r.Status = "declined"; r.ResolvedAt = state.Tick; return "Then we will leave the promise unmade. That is easier to keep.";
            case "work":
                r.Status = "waiting"; r.ReadyAt = state.Tick + 16 + r.Variant * 8; state.Stories.Repairs++;
                Record("repair", a.Place, a.Id, "You held a hinge steady while Orren worked. Tavi watched the movement of your hands.");
                return "There. Feel how it moves? Don't test it again yet. Have a walk. Tomorrow's hand can judge today's work.";
            case "food":
                state.Bread -= 2; r.Contributed = true; PrepareSupper(r);
                return "I'll put these under the cloth. There is still time to ask our guest.";
            case "invite":
                r.Invited = true; PrepareSupper(r);
                return "They asked for me? Not for someone to fill a seat? All right. I'll bring the cups back afterwards.";
            case "hear":
                r.Found = true;
                return r.Variant switch
                {
                    0 => "They said I left first. I remember holding the door for them. Perhaps we were thinking of different doors.",
                    1 => "I didn't refuse the gift. I asked whose hands had wrapped it. By then they had put it away.",
                    _ => "We agreed to meet after the bell. Nobody asked which of us would hear it. That is all I am certain of."
                };
            case "paraphrase":
                Person(r.Requester).Ties[a.Id] = Math.Max(0, Person(r.Requester).Ties[a.Id] - 1);
                CompleteRequest(r);
                return "Is that what the letter says, or what you took it to mean? I will keep the folded part as well.";
            case "side":
                Person(r.Target).Ties[r.Requester] = Math.Max(0, Person(r.Target).Ties[r.Requester] - 2);
                CompleteRequest(r);
                return "It is comforting to be believed. I wonder whether they will still come to the table.";
            default:
                if (r.Kind == "bread") { state.Bread -= 2; a.Reserve = Math.Min(20,a.Reserve+2); a.Hunger = Math.Max(0,a.Hunger-2); }
                if (r.Kind == "letter") state.Stories.DeliveredLetters++;
                if (r.Kind == "accounts") Person(r.Target).Ties[r.Requester] = Math.Min(12,Person(r.Target).Ties[r.Requester]+1);
                CompleteRequest(r);
                return r.Kind switch
                {
                    "bread" => "One for now, one for later. Sit with me for the first, if you like. It tastes less borrowed that way.",
                    "repair" => "It opens quietly now. Tavi says the room feels larger. We only changed the hinge, didn't we?",
                    "letter" => "You left it closed. Thank you. I can still hear the pause before it was written.",
                    "search" => "Yes, that is it. I knew the scratch before I knew the shape. Strange what stays, isn't it?",
                    "supper" => "They stayed to wash the cups. Next time we won't need to call it an invitation. There will just be another place.",
                    _ => "Keep both names, then. We can sit across from a different account. It is harder to sit across from silence."
                };
        }
    }
    private void PrepareSupper(NeighborhoodRequest r)
    {
        if (!r.Contributed || !r.Invited) return;
        r.Status = "waiting"; r.ReadyAt = state.Tick / 96 * 96 + 76;
        if (r.ReadyAt <= state.Tick) r.ReadyAt += 96;
    }
    private void CompleteRequest(NeighborhoodRequest r)
    {
        r.Status = "complete"; r.ResolvedAt = state.Tick;
        state.Stories.Familiarity[r.Requester] = Math.Min(12,state.Stories.Familiarity[r.Requester]+2);
        Journal(Person(r.Requester).Name + "'s note", "You kept your word. There is a place for you next time.");
    }
    private static string RequestTitle(NeighborhoodRequest r) => r.Kind switch
    { "bread" => "Two loaves for later", "repair" => "A hinge and another hand", "letter" => "The unopened letter", "search" => "A small thing left behind", "supper" => "Another place at supper", _ => "Two accounts of an afternoon" };
    private string RequestNext(NeighborhoodRequest r) => r.Status switch
    {
        "offered" => "A promise has not been made yet.",
        "waiting" => "Let some time pass, then return for a conversation.",
        "ready" => $"Come back and speak with {Person(r.Requester).Name}.",
        "complete" => "The favor is remembered. The conversation may continue.",
        "declined" => "No promise was made.",
        "settled" => "The neighbors found their own way through it.",
        _ => r.Kind switch
        {
            "bread" => "Bring two loaves to the person who asked.",
            "repair" => "Visit the mending room and lend a hand.",
            "letter" => $"The letter is for {Person(r.Target).Name}.",
            "search" => r.Found ? "Return the keepsake to the person who lost it." : $"Look around at {Places.Single(p => p.Id == r.Place).Name}.",
            "supper" => r.Invited ? "The guest is coming. Bring bread to the table keeper." : $"Invite {Person(r.Target).Name} and bring bread for supper.",
            _ => r.Found ? "Bring both accounts back to the person who asked." : $"Hear {Person(r.Target).Name} before deciding what to repeat."
        }
    };
    public ActionResult LookAround()
    {
        var found = state.Stories.Requests.FirstOrDefault(r => r.Kind == "search" && r.Known && r.Status == "accepted" && !r.Found && r.Place == state.PlayerPlace);
        if (found != null)
        {
            found.Found = true;
            var text = found.Variant switch
            { 0 => "Under the bench: a little wooden bird, worn smooth except for one scratch.", 1 => "Beside a folded cloth: a brass button with a strand of blue thread.", _ => "At the edge of the path: a small cup with a repaired handle." };
            Journal("What you saw", text); Run(1); return new(true,text);
        }
        var observations = state.PlayerPlace switch
        {
            "bakery" => new[] { "Two prices are chalked beside the same loaf. One has a person's name under it.", "The bowls are clean. A flower stands in one of them, where a receipt might have been." },
            "workshop" => new[] { "The tools have outlines on the wall. One outline is shaped like an open hand.", "Someone has mended a coat from the inside. The worn place is still visible outside." },
            "landing" => new[] { "The path divides, then meets again before the water. Both branches have fresh footprints.", "A length of blue thread is caught in the boards. It could have come from several things." },
            _ => new[] { "The empty chair has been turned a little closer to the others.", "Someone has written six names on the underside of a cup. One is crossed out very carefully." }
        };
        var detail = observations[Next(observations.Length)]; Journal("What you saw",detail); Run(1); return new(true,detail);
    }
    public ActionResult WaitUntilMorning()
    {
        var nextMorning = state.Tick / 96 * 96 + 32;
        if (nextMorning <= state.Tick) nextMorning += 96;
        Run((int)(nextMorning-state.Tick));
        return new(true,"Morning comes. There may be someone new at the table.");
    }
    private string Neighbor(Agent a)
    {
        var id = a.Ties.OrderByDescending(t => t.Value).ThenBy(t => t.Key,StringComparer.Ordinal).First().Key;
        return a.Ties[id] >= 7 ? $"{Person(id).Name} knows when to leave the second cup. That is more useful than knowing everything about me." :
            $"{Person(id).Name} and I still have things to ask each other. I would worry if we ran out.";
    }
    private string Personal(Agent a) => a.Id switch
    {
        "mara" => state.Stories.Familiarity[a.Id] >= 5 ? "I used to want a shop with my name over the door. Now I want someone to notice when I close it early." : "The oven takes longer to cool than I take to stop worrying. I think that is why I stay near it.",
        "iven" => state.Stories.Familiarity[a.Id] >= 5 ? "You remember the parcel story. Good. Some days I need someone who remembers that I was the one who told it." : "I like the part of a journey before anybody asks what arrived. For a little while, carrying is enough.",
        "sela" => state.Stories.SharedSuppers > 0 ? "After that supper, I stopped counting chairs before people came. I still put one extra out. Habit is slower than understanding." : "Someone kept a place for me once. I don't remember what they served. I remember not having to ask whether I could sit.",
        "orren" => state.Stories.Repairs > 0 ? "You held the hinge differently from Tavi. Both ways worked. I wish people were as willing to admit that about other things." : "Mending leaves a seam. I like that. A thing can be useful without pretending nothing happened to it.",
        "neri" => state.Stories.DeliveredLetters > 0 ? "You carried the quiet part of a letter. Most people only notice the words. That is why I asked you." : "I wanted to keep a perfect record. Then someone asked where the hesitation belonged. I left more space after that.",
        _ => state.Stories.Familiarity[a.Id] >= 5 ? "People have begun leaving my cup where I put it, instead of where it used to go. That is a small thing. I think it is mine." : "I thought belonging would feel like recognition. So far it feels like being allowed to ask the same question again."
    };
}
