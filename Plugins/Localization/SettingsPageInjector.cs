using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using ETS2LA.Backend.Events;
using ETS2LA.Shared;
using ETS2LA.UI.Rendering;
using ETS2LA.UI.Views;

using Optris.Icons.Avalonia;

namespace Localization;

/// <summary>
///  Injects a "Localization" tab into ETS2LA's Settings sidebar (SettingsView) and
///  renders the plugin's settings page into its content host. ETS2LA has not wired
///  up <see cref="IPluginUi"/> pages yet, so without this the plugin can be enabled
///  in the manager but never configured. Everything is done at runtime from the
///  visual tree, no ETS2LA source changes needed.
/// </summary>
public static class SettingsPageInjector
{
    private const string ButtonName = "SrlilyLocalizationButton";

    private static SettingsView? _view;
    private static StackPanel? _group;
    private static Button? _button;
    private static ContentControl? _host;
    private static Control? _rendered;
    private static int _retries;

    /// <summary>
    ///  Provides the plugin instance that renders the page. Overridable so headless
    ///  tests can point it at the instance loaded by the real PluginHandler.
    /// </summary>
    public static Func<IPluginUi?> PluginInstanceProvider { get; set; } = () => Localization.Instance;

    /// <summary>
    ///  Attaches the tab to the Settings sidebar (once). Call on the UI thread,
    ///  e.g. when the Settings page was switched to.
    /// </summary>
    public static void EnsureInjected()
    {
        try
        {
            foreach (var window in UiTranslator.GetOpenWindows())
            {
                foreach (var view in window.GetVisualDescendants().OfType<SettingsView>())
                {
                    InjectInto(view);
                }
            }
        }
        catch (Exception ex)
        {
            ETS2LA.Logging.Logger.Error($"Localization: Settings tab injection failed: {ex}");
        }
    }

    /// <summary>
    ///  Injects the tab into a specific SettingsView (idempotent). Public so the
    ///  injection can also be verified headlessly.
    /// </summary>
    public static void InjectInto(SettingsView view)
    {
        if (ReferenceEquals(view, _view))
            return;

        try
        {
            Inject(view);
        }
        catch (Exception ex)
        {
            ETS2LA.Logging.Logger.Error($"Localization: Settings tab injection failed: {ex}");
        }
    }

    /// <summary>
    ///  Retries <see cref="EnsureInjected"/> a few times on the UI thread, in case
    ///  SettingsView is not attached to the visual tree yet.
    /// </summary>
    public static void EnsureInjectedSoon()
    {
        _retries = 0;
        Dispatcher.UIThread.Post(TryInjectLater);
    }

    private static void TryInjectLater()
    {
        EnsureInjected();
        if (_view == null && _retries++ < 20)
        {
            Dispatcher.UIThread.Post(TryInjectLater, DispatcherPriority.Background);
        }
    }

    /// <summary>
    ///  Keeps the injected button's selected state aligned with native SettingsView
    ///  navigation. Native navigation does not know about this dynamically added button.
    /// </summary>
    public static void HandlePageSwitched(string page)
    {
        void UpdateSelection()
        {
            if (!string.Equals(page, "Settings.Localization", StringComparison.Ordinal))
                ClearSelection();
        }

        if (Application.Current != null && !Dispatcher.UIThread.CheckAccess())
            Dispatcher.UIThread.Post(UpdateSelection);
        else
            UpdateSelection();
    }

    /// <summary>
    ///  Removes the injected settings group and any rendered plugin page.
    /// </summary>
    public static void Detach()
    {
        if (Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(Detach).GetAwaiter().GetResult();
            return;
        }

        if (_host != null && ReferenceEquals(_host.Content, _rendered))
            _host.Content = null;

        if (_group?.Parent is Panel sidebar)
            sidebar.Children.Remove(_group);

        _button?.Classes.Remove("Selected");
        _view = null;
        _group = null;
        _button = null;
        _host = null;
        _rendered = null;
        _retries = 0;
    }

    /// <summary>
    ///  Re-renders the plugin page if it is currently shown (e.g. after a language
    ///  change, so the page text follows the active language).
    /// </summary>
    public static void RefreshIfVisible()
    {
        if (_host == null || _rendered == null || _view == null)
            return;
        if (!ReferenceEquals(_host.Content, _rendered))
            return;
        ShowPage(_view);
    }

    private static void Inject(SettingsView view)
    {
        // Anchor on the last settings group ("Additional"): its parent IS the sidebar
        // StackPanel, so we can insert a new group right after it, above the
        // User/Updates panel at the bottom.
        var anchor = view.FindControl<Button>("ExperimentsButton");
        if (anchor?.Parent is not StackPanel lastGroup || lastGroup.Parent is not StackPanel sidebar)
        {
            ETS2LA.Logging.Logger.Error("Localization: Could not find the Settings sidebar (ExperimentsButton anchor).");
            return;
        }

        var template = view.FindControl<Button>("DisplayButton");

        var group = new StackPanel { Spacing = 0 };

        group.Children.Add(new TextBlock
        {
            Text = "Localization",
            Margin = new Thickness(14, 12, 0, 6),
            FontSize = 12,
            Classes = { "Description" },
        });

        var button = new Button
        {
            Name = ButtonName,
            Classes = { "sidebar" },
            Theme = template?.Theme,
        };
        Avalonia.Automation.AutomationProperties.SetName(button, "Open Localization Settings");

        button.Content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(14)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
            Children =
            {
                new Icon { Value = "fa-language", FontSize = 14 }.WithColumn(0),
                new TextBlock
                {
                    Text = "Localization",
                    FontSize = 14,
                    FontWeight = FontWeight.Medium,
                    TextWrapping = TextWrapping.NoWrap,
                }.WithColumn(1),
            },
        };

        button.Click += (_, _) => ShowPage(view);

        _button = button;
        group.Children.Add(button);

        int index = sidebar.Children.IndexOf(lastGroup);
        sidebar.Children.Insert(index + 1, group);

        _view = view;
        _group = group;
        _host = view.FindControl<ContentControl>("ContentHost");
        ETS2LA.Logging.Logger.Info("Localization: Injected the Settings tab.");
    }

    private static void ShowPage(SettingsView view)
    {
        if (PluginInstanceProvider() is not { } instance || _host == null)
            return;

        var page = instance.RenderPages().FirstOrDefault(p => p.Location == PluginPageLocation.Settings);
        if (page == null)
            return;

        _rendered = PluginUiRenderer.RenderPage(page, instance);
        _host.Content = _rendered;
        _host.Focus();

        SelectButton();

        // Mirror SettingsView.SetSelected so the rest of the app sees the switch.
        Events.Current.Publish<string>("ETS2LA.UI.SwitchedPage", "Settings.Localization");
        Events.Current.Publish<EventArgs>("ETS2LA.UI.SwitchedPage.Settings.Localization", EventArgs.Empty);
    }

    private static void SelectButton()
    {
        _button?.Classes.Remove("Selected");

        if (_button?.Parent?.Parent is not StackPanel sidebar)
            return;

        foreach (var button in sidebar.GetVisualDescendants().OfType<Button>())
        {
            button.Classes.Remove("Selected");
        }

        _button.Classes.Add("Selected");
    }

    private static void ClearSelection()
    {
        _button?.Classes.Remove("Selected");
    }

    private static T WithColumn<T>(this T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
