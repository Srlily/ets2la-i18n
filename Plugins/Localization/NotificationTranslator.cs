using Avalonia;
using Avalonia.Threading;
using System.Collections.Concurrent;

using ETS2LA.Notifications;
using ETS2LA.UI.Notifications;

using LocalizationLibrary;

namespace Localization;

/// <summary>
///  Notifications are data objects rendered by a separate Growl template, so they
///  are not reliably covered by the normal visual-tree walk. Keep their source text
///  untouched and update the active UI copy instead.
/// </summary>
internal sealed class NotificationTranslator
{
    private readonly ConcurrentDictionary<string, NotificationText> _originals = new();
    private bool _started;

    public void Start()
    {
        if (_started)
            return;

        _started = true;
        NotificationHandler.Current.OnNotificationAdded += OnNotificationAdded;
        NotificationHandler.Current.OnNotificationUpdated += OnNotificationUpdated;
        NotificationHandler.Current.OnNotificationRemoved += OnNotificationRemoved;

        foreach (var notification in NotificationHandler.Current.GetActiveNotifications())
            Remember(notification);

        ApplyAll();
    }

    public void Stop()
    {
        if (!_started)
            return;

        var originals = _originals.ToArray();
        void RestoreSnapshot()
        {
            foreach (var pair in originals)
                Restore(pair.Key, pair.Value);
        }

        if (Application.Current != null && !Dispatcher.UIThread.CheckAccess())
            Dispatcher.UIThread.InvokeAsync(RestoreSnapshot).GetAwaiter().GetResult();
        else
            RestoreSnapshot();

        NotificationHandler.Current.OnNotificationAdded -= OnNotificationAdded;
        NotificationHandler.Current.OnNotificationUpdated -= OnNotificationUpdated;
        NotificationHandler.Current.OnNotificationRemoved -= OnNotificationRemoved;
        _originals.Clear();
        _started = false;
    }

    public void ApplyAll()
    {
        if (Application.Current == null)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyAll);
            return;
        }

        foreach (var notification in _originals.Keys.ToList())
            Apply(notification);
    }

    private void OnNotificationAdded(object? sender, Notification notification)
    {
        Remember(notification);
        ApplySoon(notification.Id);
    }

    private void OnNotificationUpdated(object? sender, Notification notification)
    {
        Remember(notification);
        ApplySoon(notification.Id);
    }

    private void OnNotificationRemoved(object? sender, string id)
    {
        _originals.TryRemove(id, out _);
    }

    private void Remember(Notification notification)
    {
        _originals[notification.Id] = new NotificationText(notification.Title, notification.Content);
    }

    private void ApplySoon(string id)
    {
        if (Application.Current == null)
            return;

        Dispatcher.UIThread.Post(() => Apply(id));
        Dispatcher.UIThread.Post(() => Apply(id), DispatcherPriority.Background);
    }

    private void Apply(string id)
    {
        if (!_originals.TryGetValue(id, out var original))
            return;

        var uiNotification = UINotificationHandler.Current.ActiveNotifications
            .FirstOrDefault(notification => notification.Id == id);
        if (uiNotification == null)
            return;

        string title = LocalizationManager.Current.Translate(original.Title);
        string content = LocalizationManager.Current.Translate(original.Content);

        uiNotification.Title = title;
        uiNotification.Content = content;
        if (uiNotification.Item != null)
        {
            uiNotification.Item.Title = title;
            uiNotification.Item.Content = content;
        }
    }

    private void Restore(string id, NotificationText original)
    {
        var uiNotification = UINotificationHandler.Current.ActiveNotifications
            .FirstOrDefault(notification => notification.Id == id);
        if (uiNotification == null)
            return;

        uiNotification.Title = original.Title;
        uiNotification.Content = original.Content;
        if (uiNotification.Item != null)
        {
            uiNotification.Item.Title = original.Title;
            uiNotification.Item.Content = original.Content;
        }
    }

    private readonly record struct NotificationText(string Title, string Content);
}
