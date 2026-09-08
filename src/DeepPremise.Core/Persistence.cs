using System.Text.Json;

namespace DeepPremise.Core;

public sealed partial class SimulationRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string SaveJson() => JsonSerializer.Serialize(state, JsonOptions);
    public static SimulationRunner LoadJson(string json)
    {
        if (json.Length > 4_000_000) throw new InvalidDataException("Save is too large.");
        WorldState? saved;
        try { saved = JsonSerializer.Deserialize<WorldState>(json, JsonOptions); }
        catch (JsonException ex) { throw new InvalidDataException("The save is not valid JSON.", ex); }
        try { Validate(saved); }
        catch (Exception ex) when (ex is NullReferenceException or ArgumentException or InvalidOperationException)
        { throw new InvalidDataException("The save contains invalid references.", ex); }
        return new SimulationRunner(saved!);
    }
    private static void Validate(WorldState? s)
    {
        void Require(bool condition) { if (!condition) throw new InvalidDataException("The save has an unsupported or invalid structure."); }
        Require(s != null);
        Require(s!.Version == 3 && s.RandomState != 0 && s.Tick is >= 0 and < 1_000_000_000);
        Require(s.NextEvent > 0 && s.NextLine > 0 && s.Bread is >= 0 and <= 16);
        Require(Places.Any(p => p.Id == s.PlayerPlace) && s.Notes.Length <= 5000);
        Require(s.Agents.Count == 6 && s.Agents.Select(a => a.Id).Order().SequenceEqual(AgentIds.Order()));
        Require(s.Events.Count <= 512 && s.Transcript.Count <= 160 && s.Journal.Count <= 100 && s.Accounts.Count <= 48);
        Require(s.Deliveries.Count <= 1 && s.Obligations.Count == 1 && s.Questions.Count <= 64);
        Require(s.Events.Select(e => e.Id).Distinct().Count() == s.Events.Count);
        Require(s.Transcript.Select(e => e.Id).Distinct().Count() == s.Transcript.Count);
        foreach (var a in s.Agents)
        {
            Require(a.Name.Length is > 0 and < 50 && a.Role.Length < 50 && a.Seat.Length < 50);
            Require(Places.Any(p => p.Id == a.Place) && Places.Any(p => p.Id == a.Home));
            Require(a.Hunger is >= 0 and <= 8 && a.Reserve is >= 0 and <= 20 && a.Knowledge.Count <= 32);
            Require(a.Ties.Count == 5 && AgentIds.Where(id => id != a.Id).All(a.Ties.ContainsKey));
            Require(a.Ties.Values.All(v => v is >= 0 and <= 12));
            foreach (var k in a.Knowledge)
                Require(k.EventId > 0 && k.EventId < s.NextEvent && k.Strength is >= 0 and <= 12 && k.Claim.Length <= 1000 && k.Source.Length <= 80 && k.LearnedAt <= s.Tick);
        }
        foreach (var e in s.Events)
            Require(e.Id > 0 && e.Id < s.NextEvent && e.Tick <= s.Tick && Places.Any(p => p.Id == e.Place) && AgentIds.Contains(e.Actor) && e.Claim.Length <= 1000);
        foreach (var line in s.Transcript)
            Require(line.Id > 0 && line.Id < s.NextLine && line.Tick <= s.Tick && line.Speaker.Length <= 80 && line.Text.Length <= 1200);
        foreach (var entry in s.Journal) Require(entry.Tick <= s.Tick && entry.Text.Length <= 1200 && entry.Source.Length <= 80);
        foreach (var entry in s.Accounts) Require(entry.EventId > 0 && entry.EventId < s.NextEvent && entry.Claim.Length <= 1000 && entry.Source.Length <= 80);
        foreach (var d in s.Deliveries)
            Require(d.Carrier == "iven" && d.Destination == "workshop" && d.Amount == 2 && d.Due >= s.Tick && d.Due <= s.Tick + 32 && d.Witness is "" or "neri");
        Require(s.Obligations[0].Seat == "east" && s.Obligations[0].Creditor == "mara" && s.Obligations[0].Amount == 2);
    }
}

public sealed class SaveStore(string directory)
{
    public string FilePath { get; } = Path.Combine(directory, "conversation-v3.json");
    public string LastNotice { get; private set; } = "";
    public SimulationRunner Open(uint seed)
    {
        if (!File.Exists(FilePath)) return new SimulationRunner(seed);
        try { return SimulationRunner.LoadJson(File.ReadAllText(FilePath)); }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            var backup = FilePath + ".bak";
            if (!File.Exists(backup)) throw new InvalidDataException("Cannot read the save. The original file has been preserved.", ex);
            var recovered = SimulationRunner.LoadJson(File.ReadAllText(backup));
            File.Copy(FilePath, FilePath + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssffff"));
            File.Copy(backup, FilePath, true);
            LastNotice = "Recovered the previous save. The damaged file was preserved.";
            return recovered;
        }
    }
    public void Archive(SimulationRunner runner)
    {
        Save(runner);
        File.Copy(FilePath, FilePath + ".archive-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssffff"));
    }
    public void Save(SimulationRunner runner)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(runner.SaveJson()); writer.Flush(); stream.Flush(true);
        }
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
    }
}
