using Godot;
using DeepPremise.Core;
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
    private string model = "";
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
    private int transcriptRevision = -1;
    private bool smoke;

    public override void _Ready()
    {
        voice = new OpenAiVoice(http);
        BuildTheme(); BuildScreen();
        DisplayServer.WindowSetMinSize(new Vector2I(1160, 840));
        GetTree().AutoAcceptQuit = false;
        var args = OS.GetCmdlineUserArgs();
        smoke = args.Contains("--smoke");
        var saveArgument = args.FirstOrDefault(a => a.StartsWith("--save-dir="));
        var folder = saveArgument != null ? saveArgument[11..] : OS.GetUserDataDir();
        saves = new SaveStore(folder);
        try
        {
            runner = saves.Open((uint)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue));
            loaded = true; notes.Text = runner.Observe().Notes;
            Save(); Refresh(true);
            if (saves.LastNotice != "") status.Text = saves.LastNotice;
            if (smoke) CallDeferred(nameof(SmokeTest));
        }
        catch (Exception ex) when (ex is IO.IOException or IO.InvalidDataException or UnauthorizedAccessException)
        {
            status.Text = "The save could not be opened. Your original files are preserved.";
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
    private Label LabelText(string text, Node parent, int size = 17, string color = "e6e1cd")
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color));
        parent.AddChild(label); return label;
    }
    private Button ButtonText(string text, Node parent, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40), SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
        clock.AutowrapMode = TextServer.AutowrapMode.Off; clock.CustomMinimumSize = new Vector2(220, 0);
        clock.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        pauseButton = ButtonText("Pause", header, () => { paused = !paused; Refresh(); });
        pauseButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; pauseButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var journalButton = ButtonText("Notebook / AI", header, OpenNotebook); journalButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; journalButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
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
        ButtonText("Sit quietly for an hour", left, () => { if (!busy && loaded) { runner.Run(4); Save(); Refresh(); } });
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
        question = new LineEdit { PlaceholderText = "Ask in your own words...", MaxLength = 400, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        input.AddChild(question); question.TextSubmitted += _ => Ask();
        ButtonText("Ask", input, Ask).SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        status = LabelText("Listen, compare accounts, and let a little time pass. Space: pause.", root, 13, "a5b09b");
        BuildNotebook();
    }
    private void BuildNotebook()
    {
        notebook = new Window { Title = "Notebook & dialogue", Size = new Vector2I(780, 680), Transient = true, Visible = false };
        AddChild(notebook); notebook.CloseRequested += () => { if (loaded) { runner.SetNotes(notes.Text); Save(); } notebook.Hide(); };
        var margin = new MarginContainer(); notebook.AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 20);
        var tabs = new TabContainer(); margin.AddChild(tabs);
        var notePage = Column(tabs); notePage.Name = "Your notes";
        LabelText("Leave room for another version.", notePage, 22);
        notes = new TextEdit { SizeFlagsVertical = SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.Boundary }; notePage.AddChild(notes);
        ButtonText("Save notebook", notePage, () => { if (loaded) { var result = runner.SetNotes(notes.Text); Save(); status.Text = result.Message; } });
        ButtonText("Start another neighborhood · archive this one", notePage, NewNeighborhood);
        var scroll = new ScrollContainer { Name = "Observations", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; tabs.AddChild(scroll);
        journal = Column(scroll); journal.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var ai = Column(tabs); ai.Name = "AI voice";
        LabelText("A voice, not a storyteller", ai, 24);
        LabelText("Optional: use OpenAI to express a resident's reply in natural language. Simulation rules still determine events, memories and actions. Local dialogue always works without AI.", ai);
        LabelText("When enabled, your question, recent conversation and that resident's known accounts are sent to OpenAI. API usage may incur charges. Maximum 20 requests per session; the key is kept only in memory.", ai, 15);
        var keyInput = new LineEdit { PlaceholderText = "OpenAI API key", Secret = true }; ai.AddChild(keyInput);
        var modelInput = new LineEdit { PlaceholderText = "Model ID from your OpenAI API account" }; ai.AddChild(modelInput);
        var aiStatus = LabelText("Disabled", ai, 15);
        ButtonText("Enable for this session", ai, () =>
        {
            if (keyInput.Text.Trim() == "" || modelInput.Text.Trim() == "") { aiStatus.Text = "Enter a key and a model ID."; return; }
            apiKey = keyInput.Text.Trim(); model = modelInput.Text.Trim(); keyInput.Text = ""; aiEnabled = true;
            aiStatus.Text = "Enabled. No request is sent until you speak to a resident."; Refresh();
        });
        ButtonText("Use local dialogue", ai, () => { apiKey = ""; aiEnabled = false; aiStatus.Text = "Disabled"; Refresh(); });
        LabelText("Without AI, typed questions use topic matching. The conversation buttons give the full local interaction set. AI wording can be imperfect; it never edits simulation state.", ai, 14, "a5b09b");
    }
    private static void Clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }
    private void Refresh(bool forceTranscript = false)
    {
        if (!loaded) return;
        var view = runner.Observe(); map.Present(view); clock.Text = view.Time + "\nTable bread: " + view.Bread;
        pauseButton.Text = paused ? "Resume" : "Pause";
        location.Text = view.PlaceName; atmosphere.Text = view.Atmosphere;
        mode.Text = busy ? "Listening..." : aiEnabled ? $"AI voice · {voice.RequestsRemaining} requests left" : "Local dialogue · simulation-driven accounts";
        Clear(nearby);
        var locals = view.Residents.Where(a => a.Place == view.Place).ToArray();
        foreach (var a in locals)
        {
            var person = ButtonText((a.Id == selected ? "• " : "") + a.Name + "  /  " + a.Role, nearby, () => { if (!busy) { selected = a.Id; Refresh(); } });
            person.Disabled = busy;
        }
        if (locals.Length == 0) LabelText("Nobody is here just now.", nearby, 15);
        var resident = view.Residents.First(a => a.Id == selected);
        var present = resident.Place == view.Place;
        speaker.Text = present ? "Across from " + resident.Name : resident.Name + " has stepped away";
        Clear(choices);
        foreach (var choice in runner.Choices(selected))
        {
            var b = ButtonText(choice.Text, choices, () => _ = Speak(choice.Id));
            b.Disabled = !choice.Enabled || busy;
        }
        question.Editable = present && !busy;
        if (forceTranscript || transcriptRevision != (view.Transcript.LastOrDefault()?.Id ?? 0))
        {
            Clear(transcript);
            foreach (var line in view.Transcript.TakeLast(30))
            {
                var card = Column(transcript, 4);
                LabelText(line.Speaker.ToUpperInvariant() + "   ·   " + SimulationRunner.FormatTime(line.Tick), card, 11, line.Speaker == "You" ? "93b5a4" : "d5b778");
                LabelText(line.Text, card, 18);
            }
            transcriptRevision = view.Transcript.LastOrDefault()?.Id ?? 0;
            CallDeferred(nameof(ScrollConversation));
        }
    }
    private async void ScrollConversation()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree()) return;
        conversationScroll.ScrollVertical = (int)conversationScroll.GetVScrollBar().MaxValue;
    }
    private void Travel(string place)
    {
        if (busy || !loaded) return;
        var result = runner.Travel(place);
        var local = runner.Observe().Residents.FirstOrDefault(a => a.Place == place);
        if (local != null) selected = local.Id;
        Save(); Refresh(); status.Text = result.Message;
    }
    private void Ask() { if (!busy && loaded && !string.IsNullOrWhiteSpace(question.Text)) _ = Speak(null); }
    private async Task Speak(string? topic)
    {
        if (busy || !loaded) return;
        var spokenTo = selected;
        var result = topic == null ? runner.Ask(spokenTo, question.Text) : runner.Talk(spokenTo, topic);
        if (!result.Success) { status.Text = result.Message; return; }
        question.Text = "";
        Save(); Refresh(true);
        if (!aiEnabled) { status.Text = "The conversation is recorded here. Your notebook keeps witnessed events and your own notes."; return; }
        var context = runner.GetDialogueContext(spokenTo);
        if (context == null) return;
        busy = true; Refresh();
        try
        {
            var voiced = await voice.RenderAsync(context, apiKey, model, lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            if (voiced.Generated) runner.ApplyVoice(context.LineId, voiced.Text);
            status.Text = voiced.Notice; Save();
        }
        finally { busy = false; if (!lifetime.IsCancellationRequested) Refresh(true); }
    }
    public override void _Process(double delta)
    {
        if (!loaded || paused || busy || notebook.Visible) return;
        elapsed += Math.Min(delta, 1);
        if (elapsed < 8) return;
        elapsed = 0; runner.Run(1); Save(); Refresh();
    }
    private void OpenNotebook()
    {
        if (!loaded || busy) return;
        notes.Text = runner.Observe().Notes;
        Clear(journal);
        foreach (var entry in runner.Observe().Journal)
        {
            LabelText(entry.Source + "  ·  " + SimulationRunner.FormatTime(entry.Tick), journal, 13, "d5b778");
            LabelText(entry.Text, journal, 17);
        }
        notebook.PopupCentered();
    }
    private void NewNeighborhood()
    {
        if (!loaded || busy) return;
        try
        {
            runner.SetNotes(notes.Text); saves.Archive(runner);
            runner = new SimulationRunner((uint)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue));
            selected = "neri"; notes.Text = ""; elapsed = 0; paused = false;
            Save(); notebook.Hide(); Refresh(true);
            status.Text = "A new beginning. Your previous neighborhood was archived beside the save.";
        }
        catch (Exception ex) when (ex is IO.IOException or UnauthorizedAccessException)
        { status.Text = "Could not archive this neighborhood. The current world is unchanged."; }
    }
    private void Save()
    {
        if (!loaded) return;
        try { saves.Save(runner); }
        catch (Exception ex) when (ex is IO.IOException or UnauthorizedAccessException)
        { status.Text = "Could not save. Check the save folder and free disk space."; }
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } && !question.HasFocus() && !notes.HasFocus())
        { paused = !paused; Refresh(); }
    }
    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest) return;
        if (loaded) { if (notebook.Visible) runner.SetNotes(notes.Text); Save(); }
        lifetime.Cancel(); GetTree().Quit();
    }
    public override void _ExitTree() { lifetime.Cancel(); http.Dispose(); lifetime.Dispose(); }
    private async void SmokeTest()
    {
        paused = true;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Speak("chair");
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/conversation-desktop.png");
        Travel("landing"); await Speak("parcel");
        Travel("bakery"); await Speak("parcel");
        OpenNotebook(); notebook.Hide();
        DisplayServer.WindowSetSize(new Vector2I(1160, 840));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/conversation-minimum.png");
        Save(); GD.Print("NATIVE C# VIEWER SMOKE PASS"); GetTree().Quit();
    }
}
