using System.Text.Json;
using System.Text.RegularExpressions;
using DeepPremise.Core;
using DeepPremise.Core.Localization;

static class LocalizationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var ko = new TextCatalog("ko"); var en = new TextCatalog("en");
        check(ko.Text("Pause") != "Pause" && en.Text("Pause") == "Pause", "English and Korean UI catalogs");
        check(TextCatalog.TopicFor("파란 꾸러미를 봤나요?") == "parcel" && TextCatalog.TopicFor("기억이 확실한가요?") == "memory" && TextCatalog.TopicFor("의자 청구서가 뭐죠?") == "chair", "Korean free-text topic matching");
        check(TextCatalog.TopicFor("제가 도울 일이 있나요?") == "help", "Korean help questions open conversation without committing a favor");
        var original = new ConversationLine(1, 32, "You", "Mara, keep my exact words.", true);
        check(ko.Line(original) == original.Text, "Player-entered text is never rewritten by localization");
        var r = new SimulationRunner(231);
        var before = r.SaveJson();
        foreach (var line in r.Observe().Transcript) _ = ko.Line(line);
        check(before == r.SaveJson(), "Language projection does not change simulation state");
        r.Talk("neri", "day");
        var context = r.GetDialogueContext("neri", "ko")!;
        check(context.Language == "ko" && !Regex.IsMatch(context.GroundedReply, "[A-Za-z]{2,}"), "Korean AI context has a localized fallback");
        r.ApplyVoice(context.LineId, "잠깐 앉으세요. 차가 아직 따뜻해요.", "ko");
        var lineAfter = r.Observe().Transcript.Last();
        check(ko.Line(lineAfter).Contains("따뜻") && en.Line(lineAfter) == lineAfter.Text, "AI variants preserve canonical reply across language switches");
        var projection = r.Observe(); projection.Transcript.Last().Voices!["ko"] = "tampered";
        check(ko.Line(r.Observe().Transcript.Last()) != "tampered", "Localized voice dictionaries are detached from world state");
        check(SimulationRunner.LoadJson(r.SaveJson()).Observe().Transcript.Last().Voices!["ko"].Contains("따뜻"), "Bilingual voices survive save reload");
        var legacy = JsonSerializer.Serialize(new SimulationRunner(1).Observe().Transcript.First());
        check(legacy.Contains("Text"), "Canonical English text retained for compatibility");
        var folder = Path.Combine(Path.GetTempPath(), "unseen-language-" + Guid.NewGuid());
        var preference = new PlayerPreferences { Language = "ko" }; preference.Save(folder);
        check(PlayerPreferences.Load(folder, "en-US").Language == "ko", "Language preference persists independently of the world");
        File.WriteAllText(Path.Combine(folder,"preferences.json"), "broken");
        check(PlayerPreferences.Load(folder, "en-US").Language == "en", "Invalid preference falls back safely");
        var leftovers = new HashSet<string>();
        void Verify(string text)
        {
            var translated = ko.Text(text);
            if (Regex.IsMatch(translated, "[A-Za-z]{2,}")) leftovers.Add(text + " => " + translated);
        }
        for (uint seed = 1; seed <= 6; seed++)
        {
            var world = new SimulationRunner(seed);
            foreach (var place in world.Observe().Places) { Verify(place.Name); Verify(place.Description); }
            for (var round = 0; round < 8; round++)
            {
                foreach (var place in world.Observe().Places)
                {
                    world.Travel(place.Id);
                    foreach (var person in world.Observe().Residents.Where(p => p.Place == place.Id))
                    {
                        Verify(person.Name); Verify(person.Role); Verify(person.Activity);
                        foreach (var choice in world.Choices(person.Id).ToArray())
                        {
                            Verify(choice.Text);
                            if (choice.Enabled) world.Talk(person.Id, choice.Id);
                        }
                    }
                    foreach (var line in world.Observe().Transcript) Verify(line.Text);
                    foreach (var entry in world.Observe().Journal) { Verify(entry.Source); Verify(entry.Text); }
                    world.Run(12);
                }
            }
        }
        check(leftovers.Count == 0, "Authored dialogue and generated observation translation coverage" + (leftovers.Count == 0 ? "" : ": " + string.Join(" | ",leftovers.Take(5))));
    }
}
