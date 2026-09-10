using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DeepPremise.Core;
using DeepPremise.Core.Localization;

static class StoryChecks
{
    public static void Run(Action<bool, string> check)
    {
        void Meet(SimulationRunner world, string id)
        {
            for (var i = 0; i < 4 && world.Choices(id).Count == 0; i++)
                world.Travel(world.Observe().Residents.Single(a => a.Id == id).Place);
            if (world.Choices(id).Count == 0) throw new Exception("Unable to meet resident in fixture.");
        }
        string Say(SimulationRunner world, string id, string action)
        {
            Meet(world, id);
            var result = world.Talk(id, action);
            if (!result.Success) throw new Exception(action + ": " + result.Message);
            return result.Message;
        }
        int Accept(SimulationRunner world, string id, string title)
        {
            Say(world, id, "help");
            var thread = world.Observe().Threads.Single(t => t.Title == title);
            Say(world, id, $"request:{thread.Id}:accept");
            return thread.Id;
        }
        var repair = new SimulationRunner(371);
        check(repair.Observe().Threads.Count == 0, "Unheard requests are absent from the player notebook");
        var repairId = Accept(repair, "orren", "A hinge and another hand");
        Say(repair, "orren", $"request:{repairId}:work");
        check(!repair.Choices("orren").Any(c => c.Id == $"request:{repairId}:finish"), "Repair has a later conversation rather than an immediate reward");
        var continued = SimulationRunner.LoadJson(repair.SaveJson());
        repair.Run(40); continued.Run(40);
        check(repair.SaveJson() == continued.SaveJson(), "Pending multi-day work preserves exact continuation after reload");
        Say(repair, "orren", $"request:{repairId}:finish");
        check(repair.Observe().Threads.Single(t => t.Id == repairId).Status == "complete", "Repair can be revisited and completed");
        var after = repair.SaveJson();
        check(!repair.Talk("orren", $"request:{repairId}:finish").Success && repair.SaveJson() == after, "Completed work cannot grant repeated effects");
        var search = new SimulationRunner(581);
        var searchId = Accept(search, "mara", "A small thing left behind");
        search.Travel("landing"); var found = search.LookAround();
        Say(search, "mara", $"request:{searchId}:finish");
        check(found.Success && search.Observe().Threads.Single(t => t.Id == searchId).Status == "complete", "Looking around finds an accepted keepsake and enables its return");
        var supper = new SimulationRunner(197);
        var supperId = Accept(supper, "sela", "Another place at supper");
        Say(supper, "sela", $"request:{supperId}:food");
        check(supper.Observe().Threads.Single(t => t.Id == supperId).Status == "accepted", "Food alone does not silently invite a guest");
        Say(supper, "tavi", $"request:{supperId}:invite");
        supper.WaitUntilMorning();
        Say(supper, "sela", $"request:{supperId}:finish");
        check(supper.Observe().Threads.Single(t => t.Id == supperId).Status == "complete", "Separate invitation and contribution create a later shared supper");
        var letter = new SimulationRunner(84);
        var letterId = Accept(letter, "neri", "The unopened letter");
        var paraphrased = SimulationRunner.LoadJson(letter.SaveJson());
        Say(letter, "orren", $"request:{letterId}:finish");
        Say(paraphrased, "orren", $"request:{letterId}:paraphrase");
        var sealedState = JsonNode.Parse(letter.SaveJson())!;
        var retoldState = JsonNode.Parse(paraphrased.SaveJson())!;
        check(sealedState["Stories"]!["DeliveredLetters"]!.GetValue<int>() == 1 && retoldState["Stories"]!["DeliveredLetters"]!.GetValue<int>() == 0 &&
            !JsonNode.DeepEquals(sealedState["Agents"], retoldState["Agents"]), "Letter delivery and paraphrase have distinct social consequences");
        var unattended = new SimulationRunner(654);
        var expiredId = Accept(unattended, "mara", "A small thing left behind");
        unattended.Run(400);
        check(unattended.Observe().Threads.Single(t => t.Id == expiredId).Status == "settled", "Neighbors resolve neglected work without waiting indefinitely for the player");
        var legacy = JsonNode.Parse(new SimulationRunner(7).SaveJson())!;
        legacy.AsObject().Remove("Stories");
        var migrated = SimulationRunner.LoadJson(legacy.ToJsonString());
        check(migrated.Choices("neri").Any(c => c.Id == "help") && migrated.Tick == 32, "Older conversation saves gain stories without losing their place or time");
        var invalid = JsonNode.Parse(repair.SaveJson())!;
        invalid["Stories"]!["Requests"]![0]!["Target"] = "missing";
        try { SimulationRunner.LoadJson(invalid.ToJsonString()); check(false, "Malformed story references rejected"); }
        catch (InvalidDataException) { check(true, "Malformed story references rejected"); }
        var ko = new TextCatalog("ko");
        var untranslated = new HashSet<string>();
        void Verify(string text) { if (Regex.IsMatch(ko.Text(text), "[A-Za-z]{2,}")) untranslated.Add(text); }
        foreach (var world in new[] { repair, search, supper, letter, paraphrased, unattended })
        {
            foreach (var line in world.Observe().Transcript) Verify(line.Text);
            foreach (var thread in world.Observe().Threads) { Verify(thread.Title); Verify(thread.NextStep); }
            foreach (var entry in world.Observe().Journal) { Verify(entry.Source); Verify(entry.Text); }
        }
        check(untranslated.Count == 0, "Multi-stage story routes have Korean coverage: " + string.Join(" | ", untranslated.Take(3)));
        var daily = new SimulationRunner(1821);
        for (var i = 0; i < 60 && daily.Observe().Accounts.Count == 0; i++)
        { Meet(daily, "neri"); daily.Talk("neri", "day"); daily.Run(4); }
        check(daily.Observe().Accounts.Count > 0, "Everyday news becomes a sourced account the player can compare and retell");
        var visits = new HashSet<string>();
        var sustained = new SimulationRunner(7654);
        for (var day = 0; day < 80; day++)
        {
            sustained.WaitUntilMorning();
            var snapshot = JsonNode.Parse(sustained.SaveJson())!;
            foreach (var pair in snapshot["Stories"]!["AfternoonVisits"]!.AsObject()) visits.Add(pair.Key + ":" + pair.Value);
        }
        var neighbors = JsonNode.Parse(sustained.SaveJson())!["Agents"]!.AsArray();
        check(visits.Count >= 20 && neighbors.Any(a => a!["Hunger"]!.GetValue<int>() < 8), "Longer worlds retain varied social visits and avoid universal permanent starvation");
        var variety = new HashSet<string>();
        for (uint seed = 1; seed <= 30; seed++)
        {
            var world = new SimulationRunner(seed);
            Accept(world, "mara", "A small thing left behind"); world.Travel("landing"); variety.Add(world.LookAround().Message);
            world.Run(2500); _ = SimulationRunner.LoadJson(world.SaveJson());
        }
        check(variety.Count == 3, "Seeded keepsakes vary while thirty long-running neighborhoods remain valid");
    }
}
