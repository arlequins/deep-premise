using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DeepPremise.Core;
using DeepPremise.Core.Diagnostics;
using DeepPremise.Core.Localization;

static class PlaytestChecks
{
    public static void Run(Action<bool, string> check)
    {
        var folder = Path.Combine(Path.GetTempPath(), "unseen-playtest-" + Guid.NewGuid());
        var a = new SimulationRunner(7181); var control = new SimulationRunner(7181);
        string session;
        long marker;
        string moment;
        using (var recorder = new PlaytestRecorder(folder, a, "test", "ko"))
        {
            session = recorder.DirectoryPath;
            recorder.Record("action", new { Topic = "help", Words = "도울 일이 있나요?" });
            a.Talk("neri", "help"); control.Talk("neri", "help");
            a.Run(20); control.Run(20);
            a.SetNotes("I expected a receipt. Why did the account change?"); control.SetNotes("I expected a receipt. Why did the account change?");
            a.ApplyVoice(a.Observe().Transcript.Last().Id, "기록에는 '쉼표'와 여백도 있어요. 🌱", "ko");
            control.ApplyVoice(control.Observe().Transcript.Last().Id, "기록에는 '쉼표'와 여백도 있어요. 🌱", "ko");
            recorder.Capture("notes-and-localized-voice");
            marker = recorder.LastSequence; moment = a.SaveJson();
            a.Run(100); control.Run(100);
            recorder.Record("feedback", new { Category = "surprise", Text = "The second account changed what I thought." });
            check(a.SaveJson() == control.SaveJson(), "Detailed playtest recording does not change simulation outcomes");
        }
        var restored = PlaytestReplay.Restore(session);
        check(restored.StateJson == a.SaveJson() && restored.VerifiedStates >= 120, "Every recorded tick reconstructs and verifies against its state hash");
        var atMoment = PlaytestReplay.Restore(session, marker);
        check(atMoment.StateJson == moment && atMoment.Sequence == marker, "A specific feedback sequence restores the exact earlier world");
        var records = File.ReadAllLines(Path.Combine(session, "events.jsonl")).Select(l => JsonNode.Parse(l)!).ToArray();
        check(records.Any(r => r["Kind"]!.GetValue<string>() == "decision" && r["Data"]!["System"]!.GetValue<string>() == "random") &&
            records.Any(r => r["Kind"]!.GetValue<string>() == "decision" && r["Data"]!["System"]!.GetValue<string>() == "memory"), "Random choices and memory reasons are retained in detailed logs");
        File.AppendAllText(Path.Combine(session, "events.jsonl"), "{\"Sequence\":");
        var crashed = PlaytestReplay.Restore(session);
        check(crashed.TruncatedTail && crashed.StateJson == a.SaveJson(), "A crash-truncated final log line preserves the last verified world");
        var left = JsonNode.Parse("{\"a\":[1,2,3],\"b\":{\"old\":1},\"null\":3}")!;
        var right = JsonNode.Parse("{\"a\":[7],\"b\":{\"new\":null},\"null\":null}")!;
        check(JsonNode.DeepEquals(right, StateDelta.Apply(left, StateDelta.Between(left, right))), "State reconstruction handles array shrinking, removals and explicit nulls");
        var ko = new TextCatalog("ko"); var untranslated = new HashSet<string>();
        void Verify(string text) { if (Regex.IsMatch(ko.Text(text), "[A-Za-z]{2,}")) untranslated.Add(text); }
        void Meet(SimulationRunner world, string id)
        {
            for (var i = 0; i < 4 && world.Choices(id).Count == 0; i++) world.Travel(world.Observe().Residents.Single(r => r.Id == id).Place);
        }
        var completed = 0; var outcomes = new HashSet<string>();
        foreach (var id in new[] { "mara", "iven", "sela", "orren", "neri", "tavi" })
        foreach (var response in new[] { "listen", "press" })
        {
            var world = new SimulationRunner(51);
            Meet(world,id); world.Talk(id,"day"); world.WaitUntilMorning(); Meet(world,id); world.Talk(id,"day");
            for (var scene = 0; scene < 2; scene++)
            {
                if (scene == 1) { world.WaitUntilMorning(); world.WaitUntilMorning(); Meet(world,id); }
                Verify(world.Talk(id,"reflection:open").Message);
                Verify(world.Talk(id,"reflection:" + response).Message);
                var saved = SimulationRunner.LoadJson(world.SaveJson());
                world.WaitUntilMorning(); saved.WaitUntilMorning(); world.Run(20); saved.Run(20);
                if (world.SaveJson() != saved.SaveJson()) throw new Exception("Reflection continuation diverged.");
                Meet(world,id);
                var result = world.Talk(id,"reflection:return");
                if (result.Success) completed++;
                Verify(result.Message); outcomes.Add(result.Message);
                var state = JsonNode.Parse(world.SaveJson())!;
                if (!state["Events"]!.AsArray().Any(e => e!["Kind"]!.GetValue<string>() == "reflection")) throw new Exception("Reflection outcome was not a world event.");
                foreach (var thread in world.Observe().Threads) { Verify(thread.Title); Verify(thread.NextStep); }
            }
        }
        check(completed == 24 && outcomes.Count >= 14, "All twelve resident conversations support two answers and a grounded later return");
        check(untranslated.Count == 0, "Personal conversation branch translation coverage: " + string.Join(" | ",untranslated.Take(3)));
        Console.WriteLine("Playtest replay fixture: " + session);
    }
}
