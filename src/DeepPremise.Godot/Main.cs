using Godot;
using DeepPremise.Core;
using DeepPremise.Core.Localization;
using DeepPremise.Core.Diagnostics;
using System.Text.Json;
using DeepPremise.Dialogue;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using IO = System.IO;

namespace DeepPremise.Godot;

public partial class Main : Control
{
    private SimulationRunner runner = null!;
    private SaveStore saves = null!;
    private readonly System.Net.Http.HttpClient http = new();
    private OpenAiVoice voice = null!;
    private readonly CancellationTokenSource lifetime = new();
    private string selected = "neri";
    private string apiKey = "";
    private string model = OpenAiVoice.ModelId;
    private TextCatalog catalog = new("en");
    private PlayerPreferences preferences = new();
    private string saveDirectory = "";
    private OptionButton languagePicker = null!;
    private bool debugSession;
    private double debugElapsed;
    private bool capturing;
    private string debugDirectory = "";
    private string T(string text) => catalog.Text(text);
    private bool aiEnabled, busy, paused;
    private bool loaded;
    private double elapsed;
    private Label clock = null!, location = null!, atmosphere = null!, speaker = null!, status = null!, mode = null!;
    private NeighborhoodView map = null!;
    private VBoxContainer nearby = null!, transcript = null!;
    private GridContainer choices = null!;
    private ScrollContainer conversationScroll = null!;
    private LineEdit question = null!;
    private Button pauseButton = null!;
    private Window notebook = null!;
    private TextEdit notes = null!;
    private VBoxContainer journal = null!;
    private VBoxContainer threads = null!;
    private VBoxContainer heardAccounts = null!;
    private int transcriptRevision = -1;
    private int scrollSettleFrames;
    private bool smoke;
    private PlaytestRecorder? playtest;
    private string recordingFailure = "";
    private TextEdit feedback = null!;
    private Label recordingStatus = null!;
    private TabContainer notebookTabs = null!;
    private int feedbackTab;

    private void StartRecording()
    {
        playtest?.Dispose();
        try
        {
            var root = debugSession ? ProjectSettings.GlobalizePath("res://artifacts/playtests") : IO.Path.Combine(saveDirectory, "playtests");
            playtest = new PlaytestRecorder(root, runner, "0.3.0", catalog.Language);
            recordingFailure = "";
        }
        catch (Exception ex) when (ex is IO.IOException or UnauthorizedAccessException)
        { playtest = null; recordingFailure = ex.GetType().Name; GD.PrintErr("Playtest recording could not start."); }
    }
    private void LogAction(string action, object? detail = null) => playtest?.Record("action", new
    { Action = action, Detail = detail, Resident = selected, Language = catalog.Language, Paused = paused, Place = runner.Observe().Place });
    private void TogglePause()
    {
        if (!loaded) return;
        paused = !paused; LogAction("pause", new { paused }); Refresh();
    }
    private void MarkMoment(string category)
    {
        LogAction("feedback", new { Category = category, Text = feedback.Text, View = runner.Observe() });
        playtest?.Capture("feedback"); feedback.Text = "";
        recordingStatus.Text = T(playtest?.Healthy == true ? "Moment recorded with the current world and conversation." : "Playtest recording is unavailable. Your game still saves normally.");
    }


    public override void _Ready()
    {
        voice = new OpenAiVoice(http);
        DisplayServer.WindowSetMinSize(new Vector2I(1160, 840));
        GetTree().AutoAcceptQuit = false;
        var args = OS.GetCmdlineUserArgs();
        smoke = args.Contains("--smoke");
        paused = args.Contains("--start-paused");
        var saveArgument = args.FirstOrDefault(a => a.StartsWith("--save-dir="));
        saveDirectory = saveArgument != null ? saveArgument[11..] : OS.GetUserDataDir();
        preferences = PlayerPreferences.Load(saveDirectory, OS.GetLocale());
        var localeOverride = args.FirstOrDefault(a => a.StartsWith("--language="));
        if (localeOverride != null && localeOverride[11..] is "en" or "ko") preferences.Language = localeOverride[11..];
        catalog = new TextCatalog(preferences.Language);
        debugSession = args.Contains("--debug-session");
        debugDirectory = ProjectSettings.GlobalizePath("res://artifacts/live");
        if (debugSession)
        {
            IO.Directory.CreateDirectory(debugDirectory);
            DisplayServer.WindowSetTitle("Unseen Order [DEBUG]");
        }
        BuildTheme(); BuildScreen();
        Resized += () => scrollSettleFrames = 4;
        saves = new SaveStore(saveDirectory);
        try
        {
            runner = saves.Open((uint)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue));
            loaded = true; notes.Text = runner.Observe().Notes;
            StartRecording();
            preferences.Save(saveDirectory); Save(); Refresh(true);
            if (saves.LastNotice != "") status.Text = T(saves.LastNotice);
            if (smoke) CallDeferred(nameof(SmokeTest));
        }
        catch (Exception ex) when (ex is IO.IOException or IO.InvalidDataException or UnauthorizedAccessException)
        {
            status.Text = T("The save could not be opened. Your original files are preserved.");
            GD.PrintErr(ex.GetType().Name + ": unable to open save.");
            LabelText("Unable to open the saved neighborhood. Please check the save folder.", transcript, 20);
        }
    }
    private void BuildTheme()
    {
        var theme = new Theme { DefaultFontSize = 17 };
        var font = new FontVariation { BaseFont = GD.Load<Font>("res://game/assets/NotoSansKR.ttf"), VariationEmbolden = 0.5f };
        font.VariationOpentype = new global::Godot.Collections.Dictionary { ["wght"] = 470 };
        theme.DefaultFont = font;
        theme.SetColor("font_color", "Label", new Color("e6e1cd"));
        foreach (var type in new[] { "Button", "LineEdit", "TextEdit" })
        {
            foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            {
                var style = PanelStyle(state == "hover" ? "354d44" : "213830", 10);
                style.BorderColor = new Color(state == "focus" ? "c9b47d" : "526353");
                style.SetBorderWidthAll(1); theme.SetStylebox(state, type, style);
            }
            theme.SetColor("font_color", type, new Color("e6e1cd"));
            theme.SetColor("font_disabled_color", type, new Color("78847b"));
        }
        Theme = theme;
    }
    private static StyleBoxFlat PanelStyle(string color, int padding)
    {
        var style = new StyleBoxFlat { BgColor = new Color(color), ContentMarginLeft = padding, ContentMarginRight = padding,
            ContentMarginTop = padding, ContentMarginBottom = padding };
        style.SetCornerRadiusAll(6); return style;
    }
    private Label LabelText(string text, Node parent, int size = 17, string color = "e6e1cd", bool translate = true)
    {
        var label = new Label { Text = translate ? T(text) : text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color));
        if (translate) label.SetMeta("source_text", text);
        parent.AddChild(label); return label;
    }
    private Button ButtonText(string text, Node parent, Action action)
    {
        var button = new Button { Text = T(text), CustomMinimumSize = new Vector2(0, 40), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        button.SetMeta("source_text", text);
        button.AddThemeFontSizeOverride("font_size", 15);
        button.Pressed += action; parent.AddChild(button); return button;
    }
    private static VBoxContainer Column(Node parent, int gap = 12)
    {
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", gap); parent.AddChild(box); return box;
    }
    private void BuildScreen()
    {
        var margin = new MarginContainer(); AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 24);
        var root = Column(margin, 16);
        var header = new HBoxContainer(); root.AddChild(header);
        var title = Column(header, 3); title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        LabelText("UNSEEN ORDER", title, 29);
        LabelText("A chair. A conversation. A different account of the same morning.", title, 14, "a5b09b");
        clock = LabelText("", header, 17);
        clock.AutowrapMode = TextServer.AutowrapMode.Off; clock.CustomMinimumSize = new Vector2(170, 0);
        clock.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        pauseButton = ButtonText("Pause", header, TogglePause);
        pauseButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; pauseButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var journalButton = ButtonText("Notebook / AI", header, OpenNotebook); journalButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; journalButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        languagePicker = new OptionButton { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        languagePicker.AddItem("English"); languagePicker.AddItem(new TextCatalog("ko").Text("Korean"));
        languagePicker.Selected = preferences.Language == "ko" ? 1 : 0;
        languagePicker.ItemSelected += index => ChangeLanguage(index == 1 ? "ko" : "en");
        header.AddChild(languagePicker);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 24); root.AddChild(body);
        var left = Column(body, 8); left.CustomMinimumSize = new Vector2(405, 0);
        map = new NeighborhoodView { CustomMinimumSize = new Vector2(405, 200), SizeFlagsVertical = SizeFlags.ExpandFill };
        map.PlaceSelected += Travel; left.AddChild(map);
        var places = new GridContainer { Columns = 2 }; places.AddThemeConstantOverride("h_separation", 8); places.AddThemeConstantOverride("v_separation", 8); left.AddChild(places);
        foreach (var item in new[] { ("courtyard", "Common table"), ("bakery", "The oven"), ("workshop", "Mending room"), ("landing", "Reed landing") })
            ButtonText(item.Item2, places, () => Travel(item.Item1));
        location = LabelText("", left, 23); atmosphere = LabelText("", left, 15, "b8bda9");
        LabelText("WITHIN EARSHOT", left, 12, "cbb17a");
        var residentsScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 105), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; left.AddChild(residentsScroll);
        nearby = Column(residentsScroll, 6); nearby.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var timeActions = new HBoxContainer(); left.AddChild(timeActions);
        ButtonText("Look around", timeActions, () => { if (!busy && loaded) { LogAction("look-around"); var result = runner.LookAround(); Save(); Refresh(); status.Text = T(result.Message); } });
        ButtonText("Sit quietly for an hour", timeActions, () => { if (!busy && loaded) { LogAction("wait", new { Ticks = 4 }); runner.Run(4); Save(); Refresh(); } });
        var rightPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; rightPanel.AddThemeStyleboxOverride("panel", PanelStyle("172724", 20)); body.AddChild(rightPanel);
        var right = Column(rightPanel, 12);
        speaker = LabelText("", right, 24);
        mode = LabelText("Local dialogue · no AI connection needed", right, 12, "a5b09b");
        conversationScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 240), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(conversationScroll);
        transcript = Column(conversationScroll, 14); transcript.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        LabelText("SOMETHING TO SAY", right, 12, "cbb17a");
        var choiceScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 180), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; right.AddChild(choiceScroll);
        choices = new GridContainer { Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        choices.AddThemeConstantOverride("v_separation", 6); choiceScroll.AddChild(choices);
        var input = new HBoxContainer(); right.AddChild(input);
        question = new LineEdit { PlaceholderText = T("Ask in your own words..."), MaxLength = 400, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        input.AddChild(question); question.TextSubmitted += _ => Ask();
        ButtonText("Ask", input, Ask).SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        status = LabelText("Listen, compare accounts, and let a little time pass. Space: pause.", root, 13, "a5b09b");
        BuildNotebook();
        Retranslate(this);
    }
    private void BuildNotebook()
    {
        notebook = new Window { Title = T("Notebook & dialogue"), Size = new Vector2I(780, 680), Transient = true, Visible = false };
        AddChild(notebook); notebook.CloseRequested += () => { if (loaded) { LogAction("notebook-close"); runner.SetNotes(notes.Text); Save(); } notebook.Hide(); };
        var margin = new MarginContainer(); notebook.AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 20);
        var tabs = new TabContainer(); notebookTabs = tabs; margin.AddChild(tabs);
        tabs.TabChanged += index => { if (loaded) LogAction("notebook-tab", new { Tab = tabs.GetTabControl((int)index).Name.ToString() }); };
        var notePage = Column(tabs); notePage.Name = "Your notes";
        LabelText("Leave room for another version.", notePage, 22);
        notes = new TextEdit { SizeFlagsVertical = SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.Boundary }; notePage.AddChild(notes);
        ButtonText("Save notebook", notePage, () => { if (loaded) { LogAction("save-notes"); var result = runner.SetNotes(notes.Text); Save(); status.Text = T(result.Message); } });
        ButtonText("Start another neighborhood · archive this one", notePage, NewNeighborhood);
        ButtonText("Rest until morning", notePage, () => { if (!busy && loaded) { LogAction("rest-until-morning"); runner.SetNotes(notes.Text); var result = runner.WaitUntilMorning(); Save(); notebook.Hide(); Refresh(); status.Text = T(result.Message); } });
        var threadScroll = new ScrollContainer { Name = "Promises", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; tabs.AddChild(threadScroll);
        threads = Column(threadScroll); threads.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var scroll = new ScrollContainer { Name = "Observations", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; tabs.AddChild(scroll);
        journal = Column(scroll); journal.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var accountsScroll = new ScrollContainer { Name = "Accounts", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; tabs.AddChild(accountsScroll);
        heardAccounts = Column(accountsScroll); heardAccounts.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var feedbackPage = Column(tabs); feedbackPage.Name = "Playtest";
        feedbackTab = tabs.GetTabCount() - 1;
        LabelText("What did you expect to happen?", feedbackPage, 23);
        LabelText("Detailed play history is saved on this computer. Mark a surprising, confusing or repetitive moment so the next development pass can revisit it. F8 opens this page.", feedbackPage, 16);
        feedback = new TextEdit { SizeFlagsVertical = SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.Boundary }; feedbackPage.AddChild(feedback);
        var marks = new HBoxContainer(); feedbackPage.AddChild(marks);
        ButtonText("Surprising", marks, () => MarkMoment("surprise"));
        ButtonText("Confusing", marks, () => MarkMoment("confusion"));
        ButtonText("Repetitive", marks, () => MarkMoment("repetition"));
        ButtonText("Other feedback", feedbackPage, () => MarkMoment("other"));
        recordingStatus = LabelText("Local playtest recording", feedbackPage, 14);
        var ai = Column(tabs); ai.Name = "AI voice";
        LabelText("A voice, not a storyteller", ai, 24);
        LabelText("Optional: use OpenAI to express a resident's reply in natural language. Simulation rules still determine events, memories and actions. Local dialogue always works without AI.", ai);
        LabelText("When enabled, your question, recent conversation and that resident's known accounts are sent to OpenAI. API usage may incur charges. Maximum 20 requests per session; the key is kept only in memory.", ai, 15);
        var keyInput = new LineEdit { PlaceholderText = T("OpenAI API key"), Secret = true }; ai.AddChild(keyInput); keyInput.SetMeta("source_placeholder", "OpenAI API key");
        LabelText("Luna only · no automatic model upgrade", ai, 15);
        var aiStatus = LabelText("Disabled", ai, 15);
        ButtonText("Enable for this session", ai, () =>
        {
            if (keyInput.Text.Trim() == "") { aiStatus.Text = T("Enter your OpenAI API key."); return; }
            LogAction("ai-enabled", new { Model = OpenAiVoice.ModelId });
            apiKey = keyInput.Text.Trim(); model = OpenAiVoice.ModelId; keyInput.Text = ""; aiEnabled = true;
            aiStatus.Text = T("Enabled. No request is sent until you speak to a resident."); Refresh();
        });
        ButtonText("Use local dialogue", ai, () => { LogAction("ai-disabled"); apiKey = ""; aiEnabled = false; aiStatus.Text = T("Disabled"); Refresh(); });
        LabelText("Without AI, typed questions use topic matching. The conversation buttons give the full local interaction set. AI wording can be imperfect; it never edits simulation state.", ai, 14, "a5b09b");
    }
    private void ChangeLanguage(string language)
    {
        if (busy) return;
        if (loaded) LogAction("language", new { From = catalog.Language, To = language });
        preferences.Language = language; catalog = new TextCatalog(language);
        Retranslate(this);
        question.PlaceholderText = T("Ask in your own words...");
        notebook.Title = T("Notebook & dialogue");
        try { preferences.Save(saveDirectory); status.Text = T("Language saved."); }
        catch (Exception ex) when (ex is IO.IOException or UnauthorizedAccessException) { status.Text = T("Could not save language preference."); }
        Refresh(true);
    }
    private void Retranslate(Node node)
    {
        if (node.HasMeta("source_text"))
        {
            var text = T(node.GetMeta("source_text").AsString());
            if (node is Label label) label.Text = text;
            else if (node is Button button) button.Text = text;
        }
        if (node is LineEdit edit && node.HasMeta("source_placeholder")) edit.PlaceholderText = T(node.GetMeta("source_placeholder").AsString());
        if (node is TabContainer tabs)
            for (var i = 0; i < tabs.GetTabCount(); i++) tabs.SetTabTitle(i, T(tabs.GetChild(i).Name));
        foreach (var child in node.GetChildren()) Retranslate(child);
    }
    private async void WriteDebugContext()
    {
        if (!debugSession || !loaded || capturing) return;
        capturing = true;
        try
        {
            var view = runner.Observe();
            var context = new
            {
                UpdatedUtc = DateTime.UtcNow, ProcessId = System.Environment.ProcessId, Version = "0.3.0",
                Language = catalog.Language, SelectedResident = selected, Place = view.Place, Tick = view.Tick,
                Paused = paused, Busy = busy, NotebookOpen = notebook.Visible,
                AiEnabled = aiEnabled, AiModel = OpenAiVoice.ModelId,
                PlaytestDirectory = playtest?.DirectoryPath, PlaytestSequence = playtest?.LastSequence,
                SaveFile = saves.FilePath, LogFile = ProjectSettings.GlobalizePath("res://artifacts/live/game.log"),
                RecentConversation = view.Transcript.TakeLast(8).Select(l => new { l.Id, l.Tick, l.Speaker, Text = catalog.Line(l) })
            };
            var target = IO.Path.Combine(debugDirectory, "context.json");
            IO.File.WriteAllText(target + ".tmp", JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true }));
            IO.File.Move(target + ".tmp", target, true);
            // Never capture the settings window: it may contain an API key in an edit field.
            if (!notebook.Visible)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (IsInsideTree()) GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(debugDirectory, "screen.png"));
            }
        }
        catch (Exception ex) when (ex is IO.IOException or UnauthorizedAccessException) { GD.PrintErr("Debug snapshot unavailable."); }
        finally { capturing = false; }
    }
    private static void Clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }
    private void Refresh(bool forceTranscript = false)
    {
        if (!loaded) return;
        var view = runner.Observe(); map.Present(view, catalog); clock.Text = catalog.Time(view.Tick) + "\n" + T("Table bread: " + view.Bread);
        pauseButton.Text = T(paused ? "Resume" : "Pause");
        location.Text = T(view.PlaceName); atmosphere.Text = T(view.Atmosphere);
        mode.Text = T(busy ? "Listening..." : aiEnabled ? $"AI voice · {voice.RequestsRemaining} requests left" : "Local dialogue · simulation-driven accounts");
        Clear(nearby);
        var locals = view.Residents.Where(a => a.Place == view.Place).ToArray();
        foreach (var a in locals)
        {
            var person = ButtonText((a.Id == selected ? "• " : "") + T(a.Name) + "  /  " + T(a.Role), nearby, () => { if (!busy) { selected = a.Id; LogAction("select-resident"); Refresh(); } });
            person.Disabled = busy;
        }
        if (locals.Length == 0) LabelText("Nobody is here just now.", nearby, 15);
        var resident = view.Residents.First(a => a.Id == selected);
        var present = resident.Place == view.Place;
        speaker.Text = T(present ? "Across from " + resident.Name : resident.Name + " has stepped away");
        Clear(choices);
        foreach (var choice in runner.Choices(selected))
        {
            var b = ButtonText(choice.Text, choices, () => _ = Speak(choice.Id));
            b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            b.Alignment = HorizontalAlignment.Left;
            b.Disabled = !choice.Enabled || busy;
        }
        question.Editable = present && !busy;
        languagePicker.Disabled = busy;
        playtest?.Record("view", new { Language = catalog.Language, SelectedResident = selected, Place = view.Place,
            VisibleLines = view.Transcript.TakeLast(30).Select(l => new { l.Id, Text = catalog.Line(l), l.Speaker }).ToArray(),
            Choices = runner.Choices(selected).Select(c => new { c.Id, Text = T(c.Text), c.Enabled }).ToArray(), Nearby = locals.Select(a => a.Id).ToArray() });
        if (recordingFailure != "" || playtest?.Healthy == false) mode.Text += " · " + T("Playtest log unavailable");
        if (debugSession) WriteDebugContext();
        if (forceTranscript || transcriptRevision != (view.Transcript.LastOrDefault()?.Id ?? 0))
        {
            Clear(transcript);
            foreach (var line in view.Transcript.TakeLast(30))
            {
                var card = Column(transcript, 4);
                LabelText(T(line.Speaker).ToUpperInvariant() + "   ·   " + catalog.Time(line.Tick), card, 11, line.Speaker == "You" ? "93b5a4" : "d5b778");
                LabelText(catalog.Line(line), card, 18, translate: false);
            }
            transcriptRevision = view.Transcript.LastOrDefault()?.Id ?? 0;
            CallDeferred(nameof(ScrollConversation));
        }
    }
    private void ScrollConversation() => scrollSettleFrames = 4;
    private void Travel(string place)
    {
        if (busy || !loaded) return;
        LogAction("travel", new { Destination = place });
        var result = runner.Travel(place);
        var local = runner.Observe().Residents.FirstOrDefault(a => a.Place == place);
        if (local != null) selected = local.Id;
        Save(); Refresh(); status.Text = T(result.Message);
    }
    private void Ask() { if (!busy && loaded && !string.IsNullOrWhiteSpace(question.Text)) _ = Speak(null); }
    private async Task Speak(string? topic)
    {
        if (busy || !loaded) return;
        var spokenTo = selected;
        LogAction("conversation", new { Topic = topic, Words = topic == null ? question.Text : null, Alternatives = runner.Choices(spokenTo) });
        var result = topic == null ? runner.Ask(spokenTo, question.Text) : runner.Talk(spokenTo, topic);
        playtest?.Record("conversation-result", new { result.Success, result.Message });
        if (!result.Success) { status.Text = T(result.Message); return; }
        question.Text = "";
        Save(); Refresh(true);
        if (!aiEnabled) { status.Text = T("The conversation is recorded here. Your notebook keeps witnessed events and your own notes."); return; }
        var context = runner.GetDialogueContext(spokenTo, catalog.Language);
        if (context == null) return;
        playtest?.Record("ai-request", new { Model = model, Context = context });
        busy = true; Refresh();
        try
        {
            var voiced = await voice.RenderAsync(context, apiKey, model, lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            playtest?.Record("ai-result", new { context.LineId, context.Language, voiced.Generated, voiced.Text, voiced.Notice });
            if (voiced.Generated) runner.ApplyVoice(context.LineId, voiced.Text, context.Language);
            status.Text = T(voiced.Notice); Save();
        }
        finally { busy = false; if (!lifetime.IsCancellationRequested) Refresh(true); }
    }
    public override void _Process(double delta)
    {
        if (scrollSettleFrames > 0)
        {
            scrollSettleFrames--;
            conversationScroll.ScrollVertical = (int)conversationScroll.GetVScrollBar().MaxValue;
        }
        if (debugSession && loaded)
        {
            debugElapsed += delta;
            if (debugElapsed >= 5) { debugElapsed = 0; WriteDebugContext(); }
        }
        if (!loaded || paused || busy || notebook.Visible) return;
        elapsed += Math.Min(delta, 1);
        if (elapsed < 8) return;
        elapsed = 0; LogAction("automatic-tick"); runner.Run(1); Save(); Refresh();
    }
    private void OpenNotebook()
    {
        if (!loaded || busy) return;
        LogAction("notebook-open");
        notes.Text = runner.Observe().Notes;
        recordingStatus.Text = T(playtest?.Healthy == true ? "Local playtest recording" : "Playtest recording is unavailable. Your game still saves normally.");
        Clear(threads);
        var openThreads = runner.Observe().Threads;
        if (openThreads.Count == 0) LabelText("Ask a neighbor whether they need a hand.", threads, 17);
        foreach (var thread in openThreads.Reverse())
        {
            LabelText(T(thread.Title) + " · " + T(thread.Requester), threads, 20, "d5b778");
            LabelText(thread.NextStep, threads, 17);
        }
        Clear(heardAccounts);
        LabelText("Keep the names beside the words.", heardAccounts, 21, "d5b778");
        var accounts = runner.Observe().Accounts;
        if (accounts.Count == 0) LabelText("Ask someone what they remember, or listen to their day.", heardAccounts, 17);
        foreach (var account in accounts.Reverse())
        {
            LabelText(T("Heard from ") + T(account.Source) + " · " + catalog.Time(account.HeardAt), heardAccounts, 14, "d5b778");
            LabelText(account.Claim, heardAccounts, 17);
        }
        Clear(journal);
        foreach (var entry in runner.Observe().Journal)
        {
            LabelText(entry.Source + "  ·  " + catalog.Time(entry.Tick), journal, 13, "d5b778");
            LabelText(entry.Text, journal, 17);
        }
        notebook.PopupCentered();
    }
    private void NewNeighborhood()
    {
        if (!loaded || busy) return;
        try
        {
            LogAction("new-neighborhood"); runner.SetNotes(notes.Text); saves.Archive(runner);
            playtest?.Capture("archive"); playtest?.Dispose();
            runner = new SimulationRunner((uint)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue));
            StartRecording();
            selected = "neri"; notes.Text = ""; elapsed = 0; paused = false;
            Save(); notebook.Hide(); Refresh(true);
            status.Text = T("A new beginning. Your previous neighborhood was archived beside the save.");
        }
        catch (Exception ex) when (ex is IO.IOException or UnauthorizedAccessException)
        { status.Text = T("Could not archive this neighborhood. The current world is unchanged."); }
    }
    private void Save()
    {
        if (!loaded) return;
        try { saves.Save(runner); playtest?.Capture("save"); }
        catch (Exception ex) when (ex is IO.IOException or UnauthorizedAccessException)
        { status.Text = T("Could not save. Check the save folder and free disk space."); }
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 })
        { OpenNotebook(); notebookTabs.CurrentTab = feedbackTab; return; }
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } && !question.HasFocus() && !notes.HasFocus())
        { TogglePause(); }
    }
    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest) return;
        if (loaded) { if (notebook.Visible) runner.SetNotes(notes.Text); Save(); }
        playtest?.Dispose(); lifetime.Cancel(); GetTree().Quit();
    }
    public override void _ExitTree() { playtest?.Dispose(); lifetime.Cancel(); http.Dispose(); lifetime.Dispose(); }
    private async void SmokeTest()
    {
        paused = true;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Speak("chair");
        await Speak("help");
        var offered = runner.Choices(selected).FirstOrDefault(c => c.Id.EndsWith(":accept", StringComparison.Ordinal));
        if (offered != null) await Speak(offered.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/conversation-" + catalog.Language + "-desktop.png");
        Travel("workshop");
        var handoff = runner.Choices(selected).FirstOrDefault(c => c.Id.EndsWith(":finish", StringComparison.Ordinal));
        await Speak(handoff?.Id ?? "parcel");
        Travel("bakery"); await Speak("parcel");
        OpenNotebook(); notebookTabs.CurrentTab = 1;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        notebook.GetViewport().GetTexture().GetImage().SavePng("res://artifacts/conversation-" + catalog.Language + "-promises.png");
        notebookTabs.CurrentTab = feedbackTab;
        feedback.Text = "Automated native smoke fixture; not player feedback."; MarkMoment("smoke");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        notebook.GetViewport().GetTexture().GetImage().SavePng("res://artifacts/conversation-" + catalog.Language + "-playtest.png");
        notebook.Hide();
        DisplayServer.WindowSetSize(new Vector2I(1160, 840));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/conversation-" + catalog.Language + "-minimum.png");
        Save(); GD.Print("NATIVE C# VIEWER SMOKE PASS"); GetTree().Quit();
    }
}
