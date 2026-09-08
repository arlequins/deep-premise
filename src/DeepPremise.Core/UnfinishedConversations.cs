namespace DeepPremise.Core;

public sealed class UnfinishedConversation
{
    public string AgentId { get; set; } = "";
    public int Scene { get; set; }
    public long OpenedAt { get; set; }
    public string Answer { get; set; } = "";
    public long AnsweredAt { get; set; }
    public bool Revisited { get; set; }
    public string Outcome { get; set; } = "";
    public long OutcomeAt { get; set; }
}

public sealed partial class SimulationRunner
{
    private void AdvanceReflections()
    {
        foreach (var c in state.Stories.Conversations.Where(c => c.Answer != "" && c.Outcome == "" && state.Tick / 96 > c.AnsweredAt / 96))
        {
            var person = Person(c.AgentId);
            var listener = state.Agents.Where(a => a.Id != person.Id && a.Place == person.Place)
                .OrderByDescending(a => person.Ties[a.Id]).ThenBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
            if (listener == null) continue;
            c.Outcome = ReflectionReturn(person, c); c.OutcomeAt = state.Tick;
            person.Ties[listener.Id] = Math.Min(12, person.Ties[listener.Id] + 1);
            Record("reflection", person.Place, person.Id, person.Name + ": " + c.Outcome);
            Trace("reflection", "follow-through", "A later day and a present listener allow the resident to try the thought.", new { person.Id, Listener = listener.Id, c.Scene, c.Answer });
        }
    }

    private void AddReflectionChoices(Agent a, List<DialogueChoice> choices)
    {
        var pending = state.Stories.Conversations.LastOrDefault(c => c.AgentId == a.Id && !c.Revisited);
        if (pending != null)
        {
            if (pending.Answer == "")
            {
                choices.Add(new("reflection:listen", "Let both possibilities stay in the conversation."));
                choices.Add(new("reflection:press", "Ask what they would trust enough to act on."));
            }
            else if (pending.Outcome != "")
                choices.Add(new("reflection:return", "Have you thought any more about what we said?"));
            return;
        }
        var count = state.Stories.Conversations.Count(c => c.AgentId == a.Id);
        if (count < 2 && state.Stories.Familiarity[a.Id] >= 2 && state.Tick / 96 >= (count == 0 ? 1 : 3))
            choices.Add(new("reflection:open", "What have you been thinking about since we last spoke?"));
    }

    private string Reflect(Agent a, string action)
    {
        if (action == "reflection:open")
        {
            var count = state.Stories.Conversations.Count(c => c.AgentId == a.Id);
            var conversation = new UnfinishedConversation { AgentId = a.Id, Scene = count, OpenedAt = state.Tick };
            state.Stories.Conversations.Add(conversation);
            return ReflectionPrompt(a, conversation.Scene);
        }
        var pending = state.Stories.Conversations.Last(c => c.AgentId == a.Id && !c.Revisited);
        if (action == "reflection:return")
        {
            pending.Revisited = true;
            var friend = a.Ties.OrderByDescending(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal).First();
            var reply = pending.Outcome;
            // The coda depends on current social life rather than the state when the conversation began.
            reply += friend.Value >= 7 ? $" I tried telling {Person(friend.Key).Name}. They stayed until I finished." :
                $" I might ask {Person(friend.Key).Name} next. We have not found the words for this together yet.";
            return reply;
        }
        pending.Answer = action == "reflection:listen" ? "listen" : "press";
        pending.AnsweredAt = state.Tick;
        var eventText = pending.Answer == "listen" ? $"You and {a.Name} left room for two versions of the same thought." :
            $"You asked {a.Name} which part of the story they would put to use.";
        Record("conversation", a.Place, a.Id, eventText);
        return ReflectionAnswer(a, pending);
    }

    private static string ReflectionTitle(UnfinishedConversation c) => (c.AgentId, c.Scene) switch
    {
        ("mara", 0) => "A price with a name", ("mara", _) => "Closing before the bread is gone",
        ("iven", 0) => "The journey without a receipt", ("iven", _) => "A road told twice",
        ("sela", 0) => "The cup nobody owns", ("sela", _) => "A place not yet taken",
        ("orren", 0) => "The seam on the inside", ("orren", _) => "Teaching the other hand",
        ("neri", 0) => "Where the pause belongs", ("neri", _) => "An account with two names",
        ("tavi", 0) => "A name older than its owner", _ => "The cup where I left it"
    };

    private string ReflectionPrompt(Agent a, int scene)
    {
        if (scene == 0) return a.Id switch
        {
            "mara" => "I wrote a price beside a loaf, then a name beneath it. The price looked wrong after that. The flour hadn't changed. What would you have erased?",
            "iven" => "Suppose I carry something all the way there and nobody can tell you I arrived. My shoulders know the journey. Would you send me again?",
            "sela" => "There is a cup everyone uses and nobody takes home. Yesterday someone called it mine. I washed it differently. I wish I knew why.",
            "orren" => "I turned a coat inside out to mend it. Its owner said it no longer felt borrowed. The seam is on the inside. Who was the repair for?",
            "neri" => "Two people gave me the same words with different pauses. I wrote identical lines. When I read them back, both said I had copied the other person.",
            _ => "Someone called me by a name that was on a bill before I came here. For a moment I almost answered. Is recognition always something you want?"
        };
        return a.Id switch
        {
            "mara" => state.SharedMeals > 0 ? "You have sat here to eat. So tell me this: if I close while bread is still on the shelf, have I left work unfinished, or made time for something else?" :
                "Sometimes I leave one loaf on the shelf so closing doesn't look like running out. I wonder who I am trying to reassure.",
            "iven" => "I told the road one way this morning and another way this afternoon. Both listeners knew where I meant. Perhaps directions are a kind of introduction.",
            "sela" => state.Stories.SharedSuppers > 0 ? "After our supper I set out an extra place without choosing who it was for. It felt less empty that way. Would you have put a name beside it?" :
                "I could invite someone, or I could leave a place open. One feels kinder until I imagine being the person outside.",
            "orren" => state.Stories.Repairs > 0 ? "You have held a hinge with me. Tomorrow Tavi may hold it another way. Should I teach the way I know, or wait to see whether the door opens?" :
                "Tavi holds the needle differently. The stitches are sound. I keep reaching to correct the hand, then remembering whose coat it is.",
            "neri" => state.Accounts.Count >= 2 ? "Your notebook has more than one voice in it now. When they disagree, do you make a margin, or choose which line goes first?" :
                "A blank margin looks like room to me. To someone else it looked as if I had left them out. How wide should a record be?",
            _ => "My cup was where I left it this morning. Nobody moved it back. I wanted to thank someone, but that would mean asking who had decided to leave it alone."
        };
    }

    private static string ReflectionAnswer(Agent a, UnfinishedConversation c)
    {
        if (c.Scene == 1) return c.Answer == "listen" ?
            "Then I will leave the question open tonight. That is different from forgetting it. Ask me tomorrow; I want to know whether I can tell the difference." :
            "All right. I will try one small thing, where somebody can answer back. Ask me tomorrow. I would rather bring you an attempt than a rule.";
        if (c.Answer == "listen") return a.Id switch
        {
            "mara" => "Leave both marks? Then the next person has to speak to me. I had thought a price was meant to save us that trouble.",
            "iven" => "You would hear the journey before judging the delivery. I can work with that. I might tell it more slowly than you expect.",
            "sela" => "Perhaps a cup can belong to the washing as much as the hand. I won't write that down yet. It sounds too settled on paper.",
            "orren" => "The coat can have changed for two reasons. Yes. I keep forgetting that a neat seam need not be a neat answer.",
            "neri" => "I can leave a gap between the lines. Someone may think it is a mistake. Perhaps they will ask instead of correcting it.",
            _ => "I can answer to the person without answering to the bill. I want that to be possible. Let me try the sentence a little longer."
        };
        return a.Id switch
        {
            "mara" => "I would ask whether they had eaten. That would tell me what to do with this loaf. It wouldn't tell me what bread is worth.",
            "iven" => "I would ask someone to meet me at the end. Not to watch my hands. To have a beginning for the next part of the story.",
            "sela" => "I would put it back where anyone could reach it. Then see who washes it. That is at least something my hands can ask.",
            "orren" => "I would ask the owner to wear it before calling it finished. Tools don't get the last word about comfort.",
            "neri" => "I would read it aloud with each of them present. If I cannot keep a pause on paper, perhaps I can give it a room.",
            _ => "I would ask who they meant before I answered. That is a small delay. I think I can afford it."
        };
    }

    private string ReflectionReturn(Agent a, UnfinishedConversation c)
    {
        if (c.Scene == 1) return c.Answer == "listen" ?
            "I left it open, as we said. Someone asked a question I hadn't left room for. Keeping two possibilities did not mean there were only two." :
            "I tried the small thing. It worked once. I nearly called that proof, then remembered you would ask me who else was there.";
        return (a.Id, c.Answer) switch
        {
            ("mara", "listen") => "Someone asked why there were two marks beside the loaf. We talked long enough that I burned the next batch a little. They took the darker piece.",
            ("mara", _) => "I asked whether they had eaten. They asked whether I had. I had not planned for the question to come back.",
            ("iven", "listen") => "I told the journey slowly. I remembered a stop I usually leave out. Nothing arrived there. Someone was pleased to see me anyway.",
            ("iven", _) => "I asked for someone at the other end. They were late. For once, waiting felt like part of arriving.",
            ("sela", "listen") => "I left the cup without a name. Someone put a flower in it. For an afternoon it wasn't waiting for anybody to drink.",
            ("sela", _) => "I put the cup within reach. Two people reached for it, then washed it together. That was not an answer I had prepared for.",
            ("orren", "listen") => "The owner showed somebody the seam. I thought they were complaining. They were showing where to start mending another coat.",
            ("orren", _) => "I asked them to wear it. They raised both arms and laughed. I had been waiting for a sentence instead of that.",
            ("neri", "listen") => "Someone wrote a question in the gap. Now the page has three voices. It is less tidy and easier to return to.",
            ("neri", _) => "I read it aloud. They interrupted at different places. For the first time, the page sounded like both of them.",
            ("tavi", "listen") => "I spoke to the person without taking the old name. They waited for me to finish. It was a longer wait than I expected to be given.",
            _ => "I asked who they meant. They looked at me before answering. I hadn't realized how often it happened in the other order."
        };
    }
}
