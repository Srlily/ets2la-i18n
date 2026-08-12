using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
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
        foreach (var window in GetOpenWindows())
            TranslateWindow(window);
    }

    /// <summary>
    ///  Translates a window and everything inside its visual tree.
    /// </summary>
    public static void TranslateWindow(Window window)
    {
        TranslateTitle(window);

        foreach (var visual in window.GetVisualDescendants())
            TranslateControl(visual);

        // Context menus and data-template content can be logical children without
        // being visual descendants of the window (especially while a popup opens).
        foreach (var logical in window.GetLogicalDescendants().OfType<Visual>())
            TranslateControl(logical);
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
