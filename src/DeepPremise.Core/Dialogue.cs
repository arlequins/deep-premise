using DeepPremise.Core.Localization;

namespace DeepPremise.Core;

public sealed record DialogueContext(int LineId, string Name, string Role, string Voice, string Place,
    string Question, string GroundedReply, IReadOnlyList<string> KnownAccounts, IReadOnlyList<ConversationLine> RecentConversation, string Language = "en");

public sealed partial class SimulationRunner
{
    public IReadOnlyList<DialogueChoice> Choices(string agentId)
    {
        var a = state.Agents.FirstOrDefault(a => a.Id == agentId);
        if (a == null || a.Place != state.PlayerPlace) return [];
        var choices = new List<DialogueChoice>
        {
            new("day", "How has your day been?"),
            new("parcel", "Have you seen the blue parcel?"),
            new("chair", "Who is the empty chair for?"),
            new("memory", "Do you remember that yourself?"),
            new("meal", "Share a loaf and sit a while.", state.Bread >= 1)
        };
        if (a.Id == "iven")
        {
            choices.Add(new("send", "Take two loaves to the mending room.", state.Bread >= 2 && state.Deliveries.Count == 0));
            choices.Add(new("witness", "Take two loaves. Ask Neri to meet you there.", state.Bread >= 2 && state.Deliveries.Count == 0));
        }
        if (a.Id == "tavi") choices.Add(new("bill", "Offer to acknowledge the east chair's bill.", state.Obligations.Any(o => !o.Acknowledged)));
        foreach (var account in state.Accounts.Where(k => k.Source != a.Name).TakeLast(2))
            choices.Add(new($"share:{account.EventId}", $"Tell {a.Name} what {account.Source} said."));
        return choices;
    }

    public ActionResult Talk(string agentId, string topic, string? ownWords = null)
    {
        var a = state.Agents.FirstOrDefault(a => a.Id == agentId);
        if (a == null || a.Place != state.PlayerPlace) return new(false, "Walk over to them first.");
        var choice = Choices(agentId).FirstOrDefault(c => c.Id == topic);
        if (choice == null || !choice.Enabled) return new(false, "That is not possible just now.");
        if (ownWords is { Length: > 400 }) return new(false, "Keep the question under 400 characters.");
        var repeated = state.Questions.Any(q => q.AgentId == agentId && q.Topic == topic && state.Tick - q.Tick < 8);
        state.Questions.Add(new RecentQuestion(agentId, topic, state.Tick));
        Line("You", ownWords ?? choice.Text, ownWords != null);
        string reply;
        switch (topic)
        {
            case "parcel": reply = Recall(a, "parcel"); break;
            case "memory": reply = Memory(a); break;
            case "chair": reply = Chair(a); break;
            case "meal":
                state.Bread--; a.Reserve = Math.Min(20, a.Reserve + 1); a.Hunger = Math.Max(0, a.Hunger - 2);
                state.SharedMeals++;
                foreach (var k in a.Knowledge) k.Strength = Math.Min(12, k.Strength + 2);
                Record("meal", a.Place, a.Id, $"You shared bread with {a.Name} and stayed for the conversation.");
                reply = a.Id == "orren" ? "You brought the heel. That is the best part. Don't let Mara hear me say that; she'll start charging for it." :
                    a.Id == "tavi" ? "Thank you. You didn't ask who used to sit here first. I appreciate that." :
                    "Break it in the middle. There. Now neither of us has to decide which piece was meant for whom.";
                break;
            case "send": case "witness":
                state.Bread -= 2;
                state.Deliveries.Add(new Delivery { Amount = 2, Due = state.Tick + 4 + Next(5), Witness = topic == "witness" ? "neri" : "" });
                reply = topic == "witness" ? "Neri as well? All right. Tell her to keep the wrapping this time. I'll take the bread." :
                    "Two loaves, the mending room. I can carry that. If they ask whether it arrived, ask who was there.";
                break;
            case "bill":
                foreach (var o in state.Obligations) o.Acknowledged = true;
                Record("account", a.Place, a.Id, "You stood beside Tavi while the east chair's bill was acknowledged.");
                reply = "Stand here a moment. Yes, beside me. There... the ink has stopped looking like someone else's trouble. Thank you.";
                break;
            default:
                if (topic.StartsWith("share:"))
                {
                    var account = state.Accounts.Last(k => k.EventId.ToString() == topic[6..] && k.Source != a.Name);
                    var e = state.Events.FirstOrDefault(e => e.Id == account.EventId);
                    var known = a.Knowledge.FirstOrDefault(k => k.EventId == account.EventId);
                    if (known is { Witnessed: true } && known.Claim != account.Claim)
                        reply = $"{account.Source} said that? I was there. {known.Claim} Please don't put those two things in the same sentence.";
                    else
                    {
                        if (e != null) Learn(a, e, account.Claim, account.Source, false);
                        reply = $"I hadn't heard it put that way. If I repeat it, I will say it came from {account.Source}, through you.";
                    }
                }
                else reply = Day(a);
                break;
        }
        if (repeated && topic is "parcel" or "memory" or "chair" or "day")
            reply = (a.Id == "mara" ? "You asked me just now. Has someone told you otherwise? " : "Again? Give me a moment. ") + reply;
        Line(a.Name, reply);
        Run(1);
        Trim();
        return new(true, reply);
    }

    public ActionResult Ask(string agentId, string question)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 400) return new(false, "Write a question of 1-400 characters.");
        var topic = TextCatalog.TopicFor(question);
        // Free text is conversation only. It cannot silently authorize a delivery or resource transfer.
        return Talk(agentId, topic, question);
    }
    private string Recall(Agent a, string kind)
    {
        var k = a.Knowledge.Where(k => state.Events.Any(e => e.Id == k.EventId && e.Kind == kind))
            .OrderByDescending(k => k.LearnedAt).FirstOrDefault();
        if (k == null)
            return a.Id == "iven" ? "I can remember the weight of it against my hip. Where I put it... no, that part won't come when you ask." :
                "A blue parcel? I don't have a story about that. Try the landing. Don't tell them I said I saw it.";
        if (!state.Accounts.Any(account => account.EventId == k.EventId && account.Source == a.Name && account.Claim == k.Claim))
            state.Accounts.Add(new PlayerAccount(k.EventId, k.Claim, a.Name, state.Tick));
        var lead = k.Strength <= 3 ? "The words are familiar, but I can't quite find myself in them. " :
            k.Witnessed ? "I was there. " : $"{k.Source} told me this; I didn't see it. ";
        return lead + k.Claim + (a.Id == "sela" ? " Would you like tea while you wait?" : "");
    }
    private string Memory(Agent a)
    {
        var k = a.Knowledge.LastOrDefault(k => k.Witnessed);
        if (k == null) return "I know who told me. That is not quite the same as remembering, is it?";
        if (k.Strength <= 4) return "A moment ago I would have said yes. Now I can remember saying it more clearly than doing it. Please don't finish the story for me.";
        return a.Id switch
        {
            "iven" => "My hands do. The rest depends on who I sit with. That sounded odd to you, didn't it? Never mind. Ask Sela what she saw.",
            "neri" => "I wrote it down before we left the table. I can show you the words. I can't lend you the morning.",
            "tavi" => "Some things feel older than I am. Orren says not to throw them away just because they don't fit yet.",
            _ => "Yes. At least, I was there when it happened. Stay a little. It is easier to tell with someone listening."
        };
    }
    private string Chair(Agent a) => a.Id switch
    {
        "tavi" => state.Obligations.Any(o => !o.Acknowledged) ?
            "Mine, they say. I arrived yesterday. This morning Mara brought me a bill from last winter. She wasn't trying to cheat me. That is the part I don't understand." :
            "It's mine now. The bill hasn't vanished, but nobody is asking me to remember buying the bread anymore. You were there. That seems to be enough.",
        "mara" => "The east chair still owes me two loaves. No, not Tavi. The chair. I wouldn't ask a stranger to remember a meal they never ate.",
        "orren" => "Tavi has the east place. I moved the coat to make room. The old one? Leave it there. Some mornings it is the right size again.",
        "sela" => "Leave it turned towards the table. An empty chair and a spare chair are very different things.",
        "neri" => "I can change the name on the bill. That isn't the difficult part. Someone has to stand beside the new name when I read it.",
        _ => "I don't sit there. Last time I did, people started thanking me for a repair I couldn't do. Orren had to come and untangle it."
    };
    private string Day(Agent a)
    {
        if (a.Hunger >= 3) return "I keep losing my place in the conversation. Have you eaten? I haven't, yet.";
        var news = a.Knowledge.LastOrDefault(k => k.Strength >= 4 && state.Tick - k.LearnedAt < 24);
        if (news != null && Next(3) == 0)
            return (news.Witnessed ? "Something small: " : $"I heard from {news.Source}: ") + news.Claim;
        var variants = a.Id switch
        {
            "mara" => new[] { "The first batch caught on the bottom. Orren calls that a crust with ambition. He still took two.", "I had an argument with the dough. It rose anyway. Sit down before I give you a job.", "Someone returned my bowl with a flower in it. No name. I prefer that kind of accounting." },
            "iven" => new[] { "I took the longer path. I was supposed to meet someone along it. I didn't. Still, it was the right path.", "A quiet morning. I have a parcel to remember and a shoulder that would rather forget it.", "Sela says I walk too fast. I say the water walks too slowly. Neither of us has convinced it." },
            "sela" => new[] { "Neri set six cups out. There were five of us. Nobody moved the sixth. Would you like it?", "I found a button under your chair. Keep it until someone misses it.", "It's a good day when everyone complains about the tea. It means they all came." },
            "orren" => new[] { "I mended the kettle. It whistles a different note now. Mara says I have repaired the wrong part.", "Tavi is good with their hands. They ask permission from the tools. The tools seem to like that.", "Hold that end, would you? There. You have now done half my morning's work." },
            "neri" => new[] { "Three letters, two signatures, one person asking whether a promise can be returned unopened.", "I keep the crossings-out. Sometimes they tell me more than the names.", "You don't have to write everything down. Leave yourself something to ask tomorrow." },
            _ => new[] { "Orren showed me how to mend a hinge. My hands knew the last part before he said it. Does that happen here?", "I slept well. Someone had left a coat my size. I haven't asked whose it was.", "I keep introducing myself. People are kind about it. Some of them look relieved." }
        };
        return variants[Next(variants.Length)];
    }
    public DialogueContext? GetDialogueContext(string agentId, string language = "en")
    {
        var a = state.Agents.FirstOrDefault(a => a.Id == agentId);
        var line = state.Transcript.LastOrDefault();
        if (a == null || line?.Speaker != a.Name) return null;
        var voice = a.Id switch
        {
            "mara" => "Practical, affectionate, dry humor. Short concrete sentences.",
            "iven" => "Restless, hesitant about recollection. Notices routes and physical sensations.",
            "sela" => "Welcoming, indirect. Treats local customs as ordinary; never lectures.",
            "orren" => "Patient, wry. Thinks through tools, mending, and touch.",
            "neri" => "Precise about who said what. Warm without offering easy certainty.",
            _ => "New to the neighborhood. Curious, slightly self-conscious. Asks ordinary questions."
        };
        var catalog = new TextCatalog(language);
        return new(line.Id, catalog.Text(a.Name), catalog.Text(a.Role), voice, catalog.Text(Places.Single(p => p.Id == a.Place).Name),
            state.Transcript.LastOrDefault(l => l.Speaker == "You")?.Text ?? "", catalog.Text(line.Text),
            a.Knowledge.Where(k => k.Strength >= 2).Select(k => $"{(k.Witnessed ? "Recollection" : "Hearsay from " + k.Source)}: {k.Claim}").ToArray(),
            state.Transcript.TakeLast(10).Select(l => l with { Text = catalog.Line(l), Voices = null }).ToArray(), catalog.Language);
    }
    public bool ApplyVoice(int lineId, string text, string language = "en")
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1200 || language is not ("en" or "ko")) return false;
        var index = state.Transcript.FindIndex(l => l.Id == lineId && l.Speaker != "You");
        if (index < 0) return false;
        var voices = new Dictionary<string, string>(state.Transcript[index].Voices ?? []) { [language] = text.Trim() };
        state.Transcript[index] = state.Transcript[index] with { Voices = voices };
        return true;
    }
}
