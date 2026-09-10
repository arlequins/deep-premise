using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeepPremise.Core.Diagnostics;

if (args.Length < 2 || args[0] is not ("review" or "restore"))
{
    Console.WriteLine("Usage: review SESSION [REPORT.md] | restore SESSION OUTPUT.json [SEQUENCE]");
    return 2;
}
try
{
    var session = Path.GetFullPath(args[1]);
    var limit = args[0] == "restore" && args.Length >= 4 ? long.Parse(args[3]) : long.MaxValue;
    var restored = PlaytestReplay.Restore(session, limit);
    if (args[0] == "restore")
    {
        if (args.Length < 3) throw new ArgumentException("Provide a new output path.");
        var output = Path.GetFullPath(args[2]);
        if (File.Exists(output)) throw new IOException("Output already exists; choose a new path to preserve existing saves.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, restored.StateJson);
        Console.WriteLine($"Restored tick {restored.Tick}, sequence {restored.Sequence}, {restored.VerifiedStates} verified states: {output}");
        return 0;
    }
    var actions = new Dictionary<string,int>(); var residents = new Dictionary<string,int>();
    var feedback = new List<string>(); var replies = new Dictionary<string,int>();
    var failures = 0; var generated = 0; var fallbacks = 0; long elapsed = 0;
    var decisions = new Dictionary<string,int>();
    var firstTick = JsonNode.Parse(File.ReadAllText(Path.Combine(session,"initial-state.json")))!["Tick"]!.GetValue<long>();
    void Count(Dictionary<string,int> dictionary,string key) => dictionary[key] = dictionary.GetValueOrDefault(key) + 1;
    using var reader = new StreamReader(new FileStream(Path.Combine(session,"events.jsonl"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite));
    while (reader.ReadLine() is { } line)
    {
        JsonNode entry;
        try { entry = JsonNode.Parse(line)!; } catch (JsonException) when (reader.EndOfStream) { break; }
        if (entry["Sequence"]!.GetValue<long>() > restored.Sequence) break;
        elapsed = Math.Max(elapsed, entry["ElapsedMs"]!.GetValue<long>());
        var kind = entry["Kind"]!.GetValue<string>(); var data = entry["Data"];
        if (kind == "action")
        {
            if (data == null) continue; var action = data["Action"]?.GetValue<string>(); if (action == null) continue; Count(actions, action);
            if (action == "conversation") Count(residents, data["Resident"]!.GetValue<string>());
            if (action == "feedback") feedback.Add($"- Sequence {entry["Sequence"]}, tick {entry["Tick"]}: **{data["Detail"]!["Category"]}** — {data["Detail"]!["Text"]}");
        }
        if (kind == "feedback") feedback.Add($"- Sequence {entry["Sequence"]}, tick {entry["Tick"]}: moment marker");
        if (kind == "conversation-result")
        {
            if (data!["Success"]!.GetValue<bool>()) Count(replies,data["Message"]!.GetValue<string>());
            else failures++;
        }
        if (kind == "decision") Count(decisions,data!["System"]!.GetValue<string>() + "/" + data["Decision"]!.GetValue<string>());
        if (kind == "ai-result") { if (data!["Generated"]!.GetValue<bool>()) generated++; else fallbacks++; }
    }
    var state = JsonNode.Parse(restored.StateJson)!;
    var report = new StringBuilder();
    report.AppendLine("# Unseen Order playtest review\n");
    report.AppendLine($"Session: `{Path.GetFileName(session)}`. Wall time: {elapsed / 60000.0:F1} minutes. World ticks: {firstTick} → {restored.Tick}.");
    report.AppendLine($"Verified state transitions: {restored.VerifiedStates}. Last sequence: {restored.Sequence}. Truncated final record: {restored.TruncatedTail}.\n");
    report.AppendLine("## Player observations\n");
    report.AppendLine(feedback.Count == 0 ? "No explicit moment markers. Do not infer surprise or satisfaction from technical variety alone." : string.Join("\n",feedback));
    report.AppendLine("\n## Conversation evidence\n");
    report.AppendLine($"Successful replies: {replies.Values.Sum()}; distinct authored replies: {replies.Count}; rejected interactions: {failures}; AI generated/fallback: {generated}/{fallbacks}.");
    foreach (var (resident,count) in residents.OrderByDescending(p => p.Value)) report.AppendLine($"- {resident}: {count} conversation attempts");
    report.AppendLine("\nMost repeated authored replies (a review signal, not an automatic quality verdict):\n");
    foreach (var (reply,count) in replies.Where(p => p.Value > 1).OrderByDescending(p => p.Value).Take(8)) report.AppendLine($"- {count}× {reply}");
    report.AppendLine("\n## Actions\n");
    foreach (var (action,count) in actions.OrderByDescending(p => p.Value)) report.AppendLine($"- {action}: {count}");
    report.AppendLine("\n## Current story state\n");
    foreach (var request in (state["Stories"]?["Requests"]?.AsArray() ?? new JsonArray()).Where(r => r!["Known"]!.GetValue<bool>()))
        report.AppendLine($"- Request {request!["Id"]}: {request["Kind"]}, {request["Requester"]} → {request["Target"]}, {request["Status"]}");
    if (state["Stories"] != null) report.AppendLine($"Personal conversations opened: {state["Stories"]!["Conversations"]!.AsArray().Count}.");
    else if(state["Version"]!.GetValue<int>() is 10 or 11) report.AppendLine($"Type3 garden: objects: {state["Objects"]!.AsArray().Count}; night: {state["Night"]}; glow: {state["DiscoveredGlow"]}; following: {state["DiscoveredFollowing"]}; seeds: {state["DiscoveredSeed"]}; bloom: {state["DiscoveredBloom"]}; trips: {state["Trips"]}; discoveries: {state["Discoveries"]}.");
    else if(state["Version"]!.GetValue<int>()==9) report.AppendLine($"Type2 city: day {state["Day"]}; residents: {state["People"]!.AsArray().Count}; treasury: {state["Funds"]}; daily income: {state["LastIncome"]}; upkeep: {state["LastCost"]}.");
    else if(state["Version"]!.GetValue<int>()==8) report.AppendLine($"Habit world: day {state["Day"]}; people: {state["People"]!.AsArray().Count}; strain: {state["Strain"]}; established: {state["Established"]}. Supplies: {state["Stock"]}.");
    else report.AppendLine($"Run version: {state["Version"]}; wave: {state["Wave"]}; phase: {state["Phase"]}; hull: {state["Hull"]}; scrap: {state["Scrap"]}; kills: {state["Kills"]}.");
    report.AppendLine("\n## Decision trace index\n");
    foreach (var (decision,count) in decisions.OrderByDescending(p => p.Value)) report.AppendLine($"- {decision}: {count}");
    report.AppendLine("\n## Next review\n");
    report.AppendLine("Read the player's markers with nearby action/view/decision records. Restore the marked sequence into a separate save directory. Compare what was visible with the stored causes. Prioritize a broken expectation that has a discoverable cause; distinguish it from missing information, repeated text, and interface friction.");
    var reportPath = Path.GetFullPath(args.Length >= 3 ? args[2] : Path.Combine(session,"review.md"));
    File.WriteAllText(reportPath,report.ToString());
    Console.WriteLine(reportPath);
    return 0;
}
catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or FormatException or InvalidOperationException)
{
    Console.Error.WriteLine(ex.Message); return 1;
}
