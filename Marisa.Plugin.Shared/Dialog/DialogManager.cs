namespace Marisa.Plugin.Shared.Dialog;

using TKey = (long? GroupId, long? SenderId);

public static class DialogManager
{
    private static readonly TimeSpan AddRetryDelay = TimeSpan.FromMilliseconds(100);

    // map (group id, user id) to handler
    private static readonly Dictionary<TKey, Dialog.MessageHandler> Handlers = new();

    public static bool TryAddDialog(TKey key, Dialog.MessageHandler handler)
    {
        lock (Handlers)
        {
            return Handlers.TryAdd(key, handler);
        }
    }

    public static async Task AddDialogAsync(TKey key, Dialog.MessageHandler handler, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (Handlers)
            {
                if (Handlers.TryAdd(key, handler))
                {
                    break;
                }
            }

            await Task.Delay(AddRetryDelay, cancellationToken);
        }
    }

    public static bool TryRestoreDialog(TKey key, Dialog.MessageHandler handler)
    {
        lock (Handlers)
        {
            if (Handlers.ContainsKey(key)) return false;
            Handlers.Add(key, handler);
            return true;
        }
    }

    public static void RemoveDialog(TKey key)
    {
        lock (Handlers)
        {
            Handlers.Remove(key);
        }
    }

    public static bool ContainsDialog(TKey key)
    {
        lock (Handlers)
        {
            return Handlers.Count != 0 && Handlers.ContainsKey(key);
        }
    }

    public static bool TryGetDialog(TKey key, out Dialog.MessageHandler? handler)
    {
        lock (Handlers)
        {
            return Handlers.TryGetValue(key, out handler);
        }
    }
}
