using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Runtime.CompilerServices;

using LocalizationLibrary;

namespace Localization;

/// <summary>
///  Walks Avalonia visual trees and replaces hardcoded English UI strings with
///  the selected language's translations. ETS2LA does not have a built-in i18n
///  mechanism, so this plugin also revisits dynamic data-template controls.
/// </summary>
public static class UiTranslator
{
    private enum TranslationSlot
    {
        Text,
        Content,
        Header,
        Placeholder,
        ToolTip,
        AutomationName,
        WindowTitle,
    }

    private sealed class ValueState
    {
        public bool HasValue;
        public string Original = string.Empty;
        public string LastTranslated = string.Empty;
    }

    private sealed class ControlState
    {
        public readonly Dictionary<TranslationSlot, ValueState> Values = new();
        public bool PropertyChangedHooked;
    }

    private static readonly ConditionalWeakTable<Control, ControlState> _controlStates = new();
    private static readonly ConditionalWeakTable<Run, ValueState> _inlineStates = new();
    private static bool _enabled;
    private static bool _restoring;

    public static void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }

    /// <summary>
    ///  All currently open windows.
    /// </summary>
    public static IEnumerable<Window> GetOpenWindows()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            return Enumerable.Empty<Window>();

        return lifetime.Windows.ToList();
    }

    /// <summary>
    ///  Translates every open window in the application.
    /// </summary>
    public static void TranslateAllWindows()
    {
        if (!_enabled)
            return;

        foreach (var window in GetOpenWindows())
            TranslateWindow(window);
    }

    /// <summary>
    ///  Translates a window and everything inside its visual tree.
    /// </summary>
    public static void TranslateWindow(Window window)
    {
        if (!_enabled)
            return;

        TranslateTitle(window);

        foreach (var visual in window.GetVisualDescendants())
            TranslateControl(visual);

        // Context menus and data-template content can be logical children without
        // being visual descendants of the window (especially while a popup opens).
        foreach (var logical in window.GetLogicalDescendants().OfType<Visual>())
            TranslateControl(logical);
    }

    /// <summary>
    ///  Restores the original source values before the plugin is disabled. Property
    ///  change hooks remain attached to live controls, so restoration is guarded from
    ///  immediately translating the values back into the selected language.
    /// </summary>
    public static void RestoreAllWindows()
    {
        if (Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(RestoreAllWindows).GetAwaiter().GetResult();
            return;
        }

        _restoring = true;
        try
        {
            foreach (var window in GetOpenWindows())
                RestoreWindow(window);
        }
        finally
        {
            _restoring = false;
        }
    }

    private static void RestoreWindow(Window window)
    {
        RestoreValue(
            window,
            TranslationSlot.WindowTitle,
            window.Title ?? string.Empty,
            original => window.SetCurrentValue(Window.TitleProperty, original));

        foreach (var visual in window.GetVisualDescendants())
            RestoreControl(visual);

        foreach (var logical in window.GetLogicalDescendants().OfType<Visual>())
            RestoreControl(logical);
    }

    private static void TranslateTitle(Window window)
    {
        if (!string.IsNullOrEmpty(window.Title))
        {
            ApplyTranslation(
                window,
                TranslationSlot.WindowTitle,
                window.Title,
                translated => window.SetCurrentValue(Window.TitleProperty, translated));
        }

        HookControl(window);
    }

    private static void TranslateControl(Visual visual)
    {
        if (!_enabled)
            return;

        var manager = LocalizationManager.Current;

        if (visual is TextBlock textBlock)
        {
            if (!string.IsNullOrEmpty(textBlock.Text))
            {
                ApplyTranslation(
                    textBlock,
                    TranslationSlot.Text,
                    textBlock.Text,
                    translated => textBlock.SetCurrentValue(TextBlock.TextProperty, translated));
            }

            if (textBlock.Inlines is { } inlines)
            {
                foreach (var inline in inlines)
                    TranslateInline(inline);
            }
        }
        else if (visual is TextBox textBox && !string.IsNullOrEmpty(textBox.PlaceholderText))
        {
            ApplyTranslation(
                textBox,
                TranslationSlot.Placeholder,
                textBox.PlaceholderText,
                translated => textBox.SetCurrentValue(TextBox.PlaceholderTextProperty, translated));
        }
        else if (visual is MenuItem menuItem && menuItem.Header is string menuHeader)
        {
            ApplyTranslation(
                menuItem,
                TranslationSlot.Header,
                menuHeader,
                translated => menuItem.SetCurrentValue(HeaderedContentControl.HeaderProperty, translated));
        }
        else if (visual is HeaderedContentControl headered && headered.Header is string header)
        {
            ApplyTranslation(
                headered,
                TranslationSlot.Header,
                header,
                translated => headered.SetCurrentValue(HeaderedContentControl.HeaderProperty, translated));
        }
        else if (visual is ContentPresenter presenter && presenter.Content is string presenterContent)
        {
            ApplyTranslation(
                presenter,
                TranslationSlot.Content,
                presenterContent,
                translated => presenter.SetCurrentValue(ContentPresenter.ContentProperty, translated));
        }
        else if (visual is ContentControl contentControl && contentControl.Content is string content)
        {
            ApplyTranslation(
                contentControl,
                TranslationSlot.Content,
                content,
                translated => contentControl.SetCurrentValue(ContentControl.ContentProperty, translated));
        }

        if (visual is not Control control)
            return;

        var toolTip = ToolTip.GetTip(control);
        if (toolTip is string tip && !string.IsNullOrEmpty(tip))
        {
            ApplyTranslation(
                control,
                TranslationSlot.ToolTip,
                tip,
                translated => control.SetCurrentValue(ToolTip.TipProperty, translated));
        }

        var automationName = AutomationProperties.GetName(control);
        if (!string.IsNullOrEmpty(automationName))
        {
            ApplyTranslation(
                control,
                TranslationSlot.AutomationName,
                automationName,
                translated => control.SetCurrentValue(AutomationProperties.NameProperty, translated));
        }

        HookControl(control);
    }

    private static void TranslateInline(Inline inline)
    {
        if (!_enabled)
            return;

        if (inline is Run run && !string.IsNullOrEmpty(run.Text))
        {
            var state = _inlineStates.GetValue(run, _ => new ValueState());
            if (!state.HasValue || run.Text != state.LastTranslated)
                state.Original = run.Text;

            var translated = LocalizationManager.Current.Translate(state.Original);
            state.HasValue = true;
            state.LastTranslated = translated;
            if (translated != run.Text)
                run.SetCurrentValue(Run.TextProperty, translated);
        }

        if (inline is Span span)
        {
            foreach (var child in span.Inlines)
                TranslateInline(child);
        }
    }

    private static void RestoreControl(Visual visual)
    {
        if (visual is TextBlock textBlock)
        {
            RestoreValue(
                textBlock,
                TranslationSlot.Text,
                textBlock.Text ?? string.Empty,
                original => textBlock.SetCurrentValue(TextBlock.TextProperty, original));

            if (textBlock.Inlines is { } inlines)
            {
                foreach (var inline in inlines)
                    RestoreInline(inline);
            }
        }
        else if (visual is TextBox textBox)
        {
            RestoreValue(
                textBox,
                TranslationSlot.Placeholder,
                textBox.PlaceholderText ?? string.Empty,
                original => textBox.SetCurrentValue(TextBox.PlaceholderTextProperty, original));
        }
        else if (visual is MenuItem menuItem && menuItem.Header is string menuHeader)
        {
            RestoreValue(
                menuItem,
                TranslationSlot.Header,
                menuHeader,
                original => menuItem.SetCurrentValue(HeaderedContentControl.HeaderProperty, original));
        }
        else if (visual is HeaderedContentControl headered && headered.Header is string header)
        {
            RestoreValue(
                headered,
                TranslationSlot.Header,
                header,
                original => headered.SetCurrentValue(HeaderedContentControl.HeaderProperty, original));
        }
        else if (visual is ContentPresenter presenter && presenter.Content is string presenterContent)
        {
            RestoreValue(
                presenter,
                TranslationSlot.Content,
                presenterContent,
                original => presenter.SetCurrentValue(ContentPresenter.ContentProperty, original));
        }
        else if (visual is ContentControl contentControl && contentControl.Content is string content)
        {
            RestoreValue(
                contentControl,
                TranslationSlot.Content,
                content,
                original => contentControl.SetCurrentValue(ContentControl.ContentProperty, original));
        }

        if (visual is not Control control)
            return;

        var toolTip = ToolTip.GetTip(control);
        if (toolTip is string tip)
        {
            RestoreValue(
                control,
                TranslationSlot.ToolTip,
                tip,
                original => control.SetCurrentValue(ToolTip.TipProperty, original));
        }

        var automationName = AutomationProperties.GetName(control);
        RestoreValue(
            control,
            TranslationSlot.AutomationName,
            automationName ?? string.Empty,
            original => control.SetCurrentValue(AutomationProperties.NameProperty, original));
    }

    private static void RestoreInline(Inline inline)
    {
        if (inline is Run run && !string.IsNullOrEmpty(run.Text)
            && _inlineStates.TryGetValue(run, out var state)
            && state.HasValue
            && run.Text == state.LastTranslated
            && run.Text != state.Original)
        {
            state.LastTranslated = state.Original;
            run.SetCurrentValue(Run.TextProperty, state.Original);
        }

        if (inline is Span span)
        {
            foreach (var child in span.Inlines)
                RestoreInline(child);
        }
    }

    private static void RestoreValue(
        Control control,
        TranslationSlot slot,
        string current,
        Action<string> restore)
    {
        if (!_controlStates.TryGetValue(control, out var controlState)
            || !controlState.Values.TryGetValue(slot, out var state)
            || !state.HasValue
            || current != state.LastTranslated
            || current == state.Original)
        {
            return;
        }

        state.LastTranslated = state.Original;
        restore(state.Original);
    }

    private static void ApplyTranslation(
        Control control,
        TranslationSlot slot,
        string current,
        Action<string> apply)
    {
        var controlState = _controlStates.GetValue(control, _ => new ControlState());
        var state = GetValueState(controlState, slot);

        if (!state.HasValue || current != state.LastTranslated)
            state.Original = current;

        var translated = LocalizationManager.Current.Translate(state.Original);
        state.HasValue = true;
        state.LastTranslated = translated;
        if (translated != current)
            apply(translated);
    }

    private static ValueState GetValueState(ControlState state, TranslationSlot slot)
    {
        if (!state.Values.TryGetValue(slot, out var value))
        {
            value = new ValueState();
            state.Values[slot] = value;
        }

        return value;
    }

    private static void HookControl(Control control)
    {
        var state = _controlStates.GetValue(control, _ => new ControlState());
        if (state.PropertyChangedHooked)
            return;

        state.PropertyChangedHooked = true;
        control.PropertyChanged += OnControlPropertyChanged;
    }

    private static void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_enabled || _restoring)
            return;

        if (sender is not Control control)
            return;

        if (e.Property == TextBlock.TextProperty
            || e.Property == TextBox.PlaceholderTextProperty
            || e.Property == ContentControl.ContentProperty
            || e.Property == ContentPresenter.ContentProperty
            || e.Property == HeaderedContentControl.HeaderProperty
            || e.Property == ToolTip.TipProperty
            || e.Property == AutomationProperties.NameProperty
            || e.Property == Window.TitleProperty)
        {
            if (control is Window window)
                TranslateTitle(window);
            else
                TranslateControl(control);
        }
    }
}
