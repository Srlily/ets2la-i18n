using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using ETS2LA.Backend;
using ETS2LA.Backend.Plugins;
using ETS2LA.Shared;
using ETS2LA.UI;
using ETS2LA.UI.Views;

using Localization;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;
using Optris.Icons.Avalonia.MaterialDesign;

int failures = 0;

void Check(bool ok, string what)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {what}");
    if (!ok) failures++;
}

// --- 1. Boot the real ETS2LA UI app (styles, resources) headlessly -----------
// SetupWithoutStarting creates the App instance and calls Initialize().
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .SetupWithoutStarting();

if (Application.Current is not App)
{
    Console.WriteLine("FAIL: ETS2LA App could not be created headlessly");
    return 1;
}

// The real app initializes Velopack before anything UI related.
Velopack.VelopackApp.Build()
    .SetAutoApplyOnStartup(false)
    .Run();

// ... and registers icon providers (also done in ETS2LA.UI/Program.cs).
IconProvider.Current
    .Register<FontAwesomeIconProvider>()
    .Register<MaterialDesignIconProvider>();

// --- 2. Construct the real SettingsView ---------------------------------------
var view = new SettingsView();
view.Measure(new Size(1200, 800));
view.Arrange(new Rect(0, 0, 1200, 800));

// --- 3. Verify the injector's anchor assumptions ------------------------------
var experiments = view.FindControl<Button>("ExperimentsButton");
Check(experiments != null, "FindControl(ExperimentsButton)");
Check(experiments?.Parent is StackPanel, $"ExperimentsButton.Parent is StackPanel (got {experiments?.Parent?.GetType().Name})");
Check(experiments?.Parent?.Parent is StackPanel, $"ExperimentsButton.Parent.Parent is StackPanel (got {experiments?.Parent?.Parent?.GetType().Name})");

var contentHost = view.FindControl<ContentControl>("ContentHost");
Check(contentHost != null, "FindControl(ContentHost)");

// --- 4. Run the real injection ------------------------------------------------
SettingsPageInjector.InjectInto(view);

// The visual tree does not materialize headlessly, so locate the injected button
// through the logical tree (sidebar StackPanel -> group StackPanel -> button).
Button? FindInjectedButton()
{
    if (experiments?.Parent is not StackPanel lastGroup || lastGroup.Parent is not StackPanel sidebar)
        return null;
    foreach (var child in sidebar.Children.OfType<StackPanel>())
    {
        var button = child.Children.OfType<Button>().FirstOrDefault(b => b.Name == "SrlilyLocalizationButton");
        if (button != null)
            return button;
    }
    return null;
}

var injectedButton = FindInjectedButton();
Check(injectedButton != null, "Injected button found in the sidebar");

// Insert order check: Localization group must sit right after the Additional group
if (experiments?.Parent is StackPanel lastGroup && injectedButton?.Parent is StackPanel group
    && lastGroup.Parent is StackPanel sidebar)
{
    int addIdx = sidebar.Children.IndexOf(lastGroup);
    int locIdx = sidebar.Children.IndexOf(group);
    Check(locIdx == addIdx + 1, $"Inserted right after Additional group (idx {locIdx} == {addIdx} + 1)");
}
else
{
    Check(false, "Insert order check ran");
}

// --- 5. Enable the real plugin so the tab has a page to render ----------------
var handler = new PluginHandler();
handler.LoadLibraries();
handler.LoadPlugins();
var plugin = handler.LoadedPlugins.FirstOrDefault(p => p.Info.Id == "srlily.i18n");
Check(plugin != null, "Plugin loaded by PluginHandler");
if (plugin != null)
{
    Check(handler.EnablePlugin(plugin), "Plugin enabled");
    Check(plugin is IPluginUi, "Loaded plugin implements IPluginUi");

    // Point the injector at the instance the PluginHandler actually loaded.
    SettingsPageInjector.PluginInstanceProvider = () => (IPluginUi)plugin;
}

// --- 6. Click the injected button, expect the plugin page in ContentHost ------
if (injectedButton != null)
{
    injectedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    var content = contentHost?.Content;
    Check(content is ScrollViewer, $"Click rendered plugin page in ContentHost (got {content?.GetType().Name})");
    Check(injectedButton.Classes.Contains("Selected"), "Injected button marked Selected");
    Check(view.FindControl<Button>("DisplayButton")?.Classes.Contains("Selected") != true, "Native button deselected");

    if (content is ScrollViewer { Content: StackPanel stack })
    {
        var texts = stack.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Check(texts.Any(t => t is "Localization" or "本地化"), "Page contains localization title");
        Check(texts.Any(t => t is "Language" or "语言"), "Page contains language selector");
        Check(!texts.Contains("Translate window titles"), "Page does not contain redundant Display options");
    }
    else
    {
        Check(false, "Page structure check ran");
    }
}

Console.WriteLine(failures == 0 ? "ALL PASSED" : $"{failures} FAILURE(S)");
Environment.Exit(failures == 0 ? 0 : 1);
return failures == 0 ? 0 : 1;
