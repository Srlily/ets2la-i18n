using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using LocalizationLibrary;

namespace Localization;

/// <summary>
///  Injects a language selector (ComboBox) into ETS2LA's sidebar, right above the
///  Settings button. ETS2LA has not wired up <see cref="IPluginUi"/> pages yet,
///  so this gives users a visible way to switch languages until then.
/// </summary>
public static class LanguageSelector
{
    private const string ComboName = "SrlilyLanguageCombo";

    private static StackPanel? _panel;
    private static ComboBox? _combo;

    /// <summary>
    ///  Attaches the selector to the given window (once) and syncs its selection.
    ///  Call on the UI thread. Does nothing while the sidebar selector is disabled
    ///  in the plugin settings.
    /// </summary>
    public static void EnsureAttached(Window window)
    {
        if (!LocalizationManager.Current.ShowSidebarSelector)
            return;

        if (_combo != null && _combo.IsAttachedToVisualTree())
        {
            Sync();
            return;
        }

        if (_panel == null)
        {
            var settingsButton = window.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(b => b.Name == "SettingsButton");
            if (settingsButton?.Parent is not StackPanel anchor)
                return;

            _panel = BuildPanel();
            anchor.Children.Insert(Math.Max(0, anchor.Children.Count - 1), _panel);
        }

        Sync();
    }

    /// <summary>
    ///  Shows or hides the injected selector. Call on the UI thread.
    /// </summary>
    public static void SetVisible(bool visible)
    {
        if (_panel == null)
            return;

        _panel.IsVisible = visible;
    }

    /// <summary>
    ///  Removes the injected selector when the localization plugin is disabled.
    /// </summary>
    public static void Detach()
    {
        if (Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(Detach).GetAwaiter().GetResult();
            return;
        }

        if (_panel?.Parent is Panel parent)
            parent.Children.Remove(_panel);

        _combo = null;
        _panel = null;
    }

    /// <summary>
    ///  Keeps the combo selection in sync with the active language. Should be called
    ///  after every language change. Call on the UI thread.
    /// </summary>
    public static void Sync()
    {
        if (_combo == null)
            return;

        var manager = LocalizationManager.Current;
        var desired = _combo.ItemsSource?.Cast<Language>()
            .FirstOrDefault(l => l.Code == manager.CurrentLanguage.Code);
        if (desired != null && !ReferenceEquals(_combo.SelectedItem, desired))
            _combo.SelectedItem = desired;
    }

    private static StackPanel BuildPanel()
    {
        var panel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(6, 0, 6, 0),
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Language",
            FontSize = 12,
            Margin = new Thickness(15, 0, 0, 0),
            Classes = { "Description" },
            TextWrapping = TextWrapping.NoWrap,
        });

        var combo = new ComboBox
        {
            Name = ComboName,
            ItemsSource = LocalizationManager.Current.Languages.ToList(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        combo.SelectionChanged += (_, e) =>
        {
            if (e.AddedItems?.Count > 0 && e.AddedItems[0] is Language language)
                LocalizationManager.Current.SetLanguage(language.Code);
        };

        _combo = combo;
        panel.Children.Add(combo);
        return panel;
    }
}
