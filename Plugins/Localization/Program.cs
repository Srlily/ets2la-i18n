using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

using ETS2LA.Backend.Events;
using ETS2LA.Shared;
using LocalizationLibrary;

namespace Localization;

public class Localization : Plugin, IPluginUi
{
    public const string WindowOpenedTopic = "ETS2LA.UI.WindowOpened";
    public const string PageSwitchedTopic = "ETS2LA.UI.SwitchedPage";
    public const string SettingsSwitchedTopic = "ETS2LA.UI.SwitchedPage.Settings";
    public const string SetLanguageTopic = "srlily.i18n.setlanguage";

    /// <summary>
    ///  The active plugin instance, for the injected Settings tab.
    /// </summary>
    public static Localization? Instance { get; private set; }

    private readonly NotificationTranslator _notificationTranslator = new();
    private DispatcherTimer? _translationTimer;

    public override PluginInformation Info => new PluginInformation
    {
        Id = "srlily.i18n",
        Version = "1.1.0",
        Name = "Localization",
        Description = "Translates the ETS2LA interface into your language. Ships with 简体中文 (Simplified Chinese).",
        AuthorName = "Srlily",
        SupportedETS2LA = "*",
        Icon = "avares://srlily.i18n/Assets/favicon.ico",
        Dependencies = new List<string>
        {
            "srlily.i18n.library"
        },
        Tags = new[] { "Localization", "Translation", "i18n" },
    };

    // This plugin works exclusively off events, keep the tick cost minimal.
    public override float TickRate => 0.1f;

    public override void Init()
    {
        base.Init();
        Instance = this;
        LocalizationManager.Current.Initialize();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        LocalizationManager.Current.LanguageChanged += OnLanguageChanged;
        Events.Current.Subscribe<EventArgs>(WindowOpenedTopic, OnWindowOpened);
        Events.Current.Subscribe<string>(PageSwitchedTopic, OnPageSwitched);
        Events.Current.Subscribe<EventArgs>(SettingsSwitchedTopic, OnSettingsSwitched);
        Events.Current.Subscribe<string>(SetLanguageTopic, OnSetLanguageRequest);
        _notificationTranslator.Start();
        StartTranslationLoop();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Events.Current.Unsubscribe<EventArgs>(WindowOpenedTopic, OnWindowOpened);
        Events.Current.Unsubscribe<string>(PageSwitchedTopic, OnPageSwitched);
        Events.Current.Unsubscribe<EventArgs>(SettingsSwitchedTopic, OnSettingsSwitched);
        Events.Current.Unsubscribe<string>(SetLanguageTopic, OnSetLanguageRequest);
        LocalizationManager.Current.LanguageChanged -= OnLanguageChanged;
        _notificationTranslator.Stop();
        StopTranslationLoop();
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        OnDisable();
    }

    // --- Events -----------------------------------------------------------

    private void OnWindowOpened(EventArgs _)
    {
        // Runs on the UI thread (published by the main window's Opened event).
        foreach (var window in UiTranslator.GetOpenWindows())
        {
            LanguageSelector.EnsureAttached(window);
            UiTranslator.TranslateWindow(window);
        }
        _notificationTranslator.ApplyAll();
        SettingsPageInjector.EnsureInjected();
    }

    private void OnSettingsSwitched(EventArgs _)
    {
        // ShowPage publishes this before attaching SettingsView, so defer one turn
        // and retry until it is in the visual tree.
        SettingsPageInjector.EnsureInjectedSoon();
    }

    private void OnPageSwitched(string page)
    {
        // Pages are attached to the window after this event, translate them once
        // they are in the tree so newly opened views follow the active language.
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var window in UiTranslator.GetOpenWindows())
                UiTranslator.TranslateWindow(window);
            SettingsPageInjector.EnsureInjectedSoon();
        });
    }

    private void OnLanguageChanged(Language _)
    {
        // May be fired from any thread, make sure we touch the UI thread.
        Dispatcher.UIThread.Post(() =>
        {
            ApplyCurrentLanguage();
            LanguageSelector.Sync();
            SettingsPageInjector.RefreshIfVisible();
        });
    }

    private void OnSetLanguageRequest(string code)
    {
        LocalizationManager.Current.SetLanguage(code);
    }

    private void StartTranslationLoop()
    {
        if (Avalonia.Application.Current == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_translationTimer != null)
                return;

            _translationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _translationTimer.Tick += (_, _) => ApplyCurrentLanguage();
            _translationTimer.Start();

            ApplyCurrentLanguage();
            SettingsPageInjector.EnsureInjectedSoon();
        });
    }

    private void StopTranslationLoop()
    {
        if (Avalonia.Application.Current == null)
            return;

        void Stop()
        {
            if (_translationTimer == null)
                return;

            _translationTimer.Stop();
            _translationTimer = null;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Stop();
        else
            Dispatcher.UIThread.Post(Stop);
    }

    private void ApplyCurrentLanguage()
    {
        UiTranslator.TranslateAllWindows();
        _notificationTranslator.ApplyAll();
    }

    // --- Plugin settings page ----------------------------------------------

    public IEnumerable<PluginPage> RenderPages()
    {
        var t = LocalizationManager.Current.Translate;
        var manager = LocalizationManager.Current;
        var languages = manager.Languages;
        var options = languages.Select(FormatLanguageOption).ToList();
        var selectedIndex = Math.Max(0, languages.ToList().FindIndex(l => l.Code == manager.CurrentLanguage.Code));
        var applyNow = "Translations are applied to the interface immediately after selection.";

        yield return new PluginPage(
            "Language",
            PluginPageLocation.Settings,
            t("Localization"),
            t("Select your interface language."),
            new UiElement[]
            {
                new UiSection(
                    t("Language"),
                    t(applyNow),
                    new UiElement[]
                    {
                        new UiCombobox(
                            t("Language"),
                            t(applyNow),
                            options,
                            selectedIndex,
                            "setlanguage"),
                        new UiText(
                            $"{t("Language packs available")}: {languages.Count}. " +
                            t("Missing translations fall back to English."),
                            Muted: true),
                    }),
            });
    }

    public void OnAction(string actionId, object? value)
    {
        var manager = LocalizationManager.Current;

        switch (actionId)
        {
            case "setlanguage" when value is string option:
            {
                var selected = manager.Languages
                    .FirstOrDefault(l => FormatLanguageOption(l) == option);
                if (selected != null)
                    manager.SetLanguage(selected.Code);
                break;
            }
        }
    }

    private static string FormatLanguageOption(Language language) =>
        $"{language.EnglishName} ({language.Name})";
}
