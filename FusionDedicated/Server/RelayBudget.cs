namespace FusionDedicated.Server;

/// <summary>
/// Caps how much traffic one player may push into the relay.
///
/// The relay multiplies: whatever one client sends is forwarded to every other
/// client, so a single hostile connection can saturate the whole lobby's
/// downlink — the same "everyone drops at once" failure a spawn flood causes,
/// with none of the spawns. The spawn guard already owns the spawn channel;
/// this covers everything else that gets rebroadcast: metadata, RPC, module
/// messages and pose updates.
///
/// The default ceiling sits well above what legitimate play produces —
/// pose updates for a crowded world land an order of magnitude below it — so
/// tripping it means a broken or hostile client, not an enthusiastic one.
/// </summary>
public sealed class RelayBudget
{
    private sealed class Tracker
    {
        public readonly Queue<(DateTime At, int Bytes)> Recent = new();
    }

    private readonly int _bytesPerWindow;
    private readonly int _windowSeconds;
    private readonly Dictionary<byte, Tracker> _byPlayer = new();
    private readonly object _lock = new();

    public RelayBudget(int kilobytesPerSecond, int windowSeconds = 5)
    {
        _bytesPerWindow = kilobytesPerSecond * 1024;
        _windowSeconds = Math.Max(1, windowSeconds);
    }

    public bool Enabled => _bytesPerWindow > 0;

    /// <summary>The last overrun, for the caller to log. Null when the message passed.</summary>
    public string? LastOverrun { get; private set; }

    public void Forget(byte smallId)
    {
        lock (_lock)
        {
            _byPlayer.Remove(smallId);
        }
    }

    /// <summary>
    /// Records <paramref name="bytes"/> and answers whether the message may go out.
    /// An over-budget message is dropped, not queued — the sender is already past
    /// what the lobby can carry on their behalf.
    /// </summary>
    public bool Allow(byte smallId, int bytes)
    {
        if (!Enabled)
        {
            LastOverrun = null;
            return true;
        }

        lock (_lock)
        {
            if (!_byPlayer.TryGetValue(smallId, out var tracker))
            {
                tracker = new Tracker();
                _byPlayer[smallId] = tracker;
            }

            var now = DateTime.UtcNow;
            var window = TimeSpan.FromSeconds(_windowSeconds);

            while (tracker.Recent.Count > 0 && now - tracker.Recent.Peek().At > window)
            {
                tracker.Recent.Dequeue();
            }

            int used = tracker.Recent.Sum(e => e.Bytes);

            if (used + bytes > _bytesPerWindow)
            {
                LastOverrun = $"{smallId} pushed {used + bytes} bytes in {_windowSeconds}s, " +
                              $"over the {_bytesPerWindow / 1024} KB relay budget";
                return false;
            }

            tracker.Recent.Enqueue((now, bytes));
            LastOverrun = null;
            return true;
        }
    }
}
