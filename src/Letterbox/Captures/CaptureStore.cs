using System.Globalization;

namespace Letterbox.Captures;

public sealed class CaptureStore
{
    readonly object _gate = new();
    readonly LinkedList<Capture> _items = new();
    readonly int _capacity;
    int _sequence;

    public CaptureStore(int capacity) => _capacity = Math.Max(1, capacity);

    public string NextId() => Interlocked.Increment(ref _sequence).ToString(CultureInfo.InvariantCulture);

    public void Add(Capture capture)
    {
        lock (_gate)
        {
            _items.AddLast(capture);
            while (_items.Count > _capacity)
                _items.RemoveFirst();
        }
    }

    public Capture? Get(string id)
    {
        lock (_gate)
            return _items.FirstOrDefault(capture => capture.Id == id);
    }

    public IReadOnlyList<Capture> List(Channel? channel, string? search, int limit)
    {
        lock (_gate)
        {
            IEnumerable<Capture> query = _items.Reverse();
            if (channel is not null)
                query = query.Where(capture => capture.Channel == channel);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(capture => Matches(capture, search));
            return query.Take(Math.Max(1, limit)).ToList();
        }
    }

    public bool Delete(string id)
    {
        lock (_gate)
        {
            var node = _items.First;
            while (node is not null)
            {
                if (node.Value.Id == id)
                {
                    _items.Remove(node);
                    return true;
                }
                node = node.Next;
            }
            return false;
        }
    }

    public int Clear()
    {
        lock (_gate)
        {
            var count = _items.Count;
            _items.Clear();
            return count;
        }
    }

    static bool Matches(Capture capture, string search)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return Contains(capture.Subject, search, comparison)
            || Contains(capture.HeaderFrom, search, comparison)
            || Contains(capture.HeaderTo, search, comparison)
            || Contains(capture.EnvelopeFrom, search, comparison)
            || string.Join(", ", capture.EnvelopeTo).Contains(search, comparison)
            || Contains(capture.PlainBody, search, comparison)
            || Contains(capture.HtmlBody, search, comparison)
            || Contains(capture.RawBody, search, comparison)
            || capture.Fields.Values.Any(value => value.Contains(search, comparison));
    }

    static bool Contains(string? text, string search, StringComparison comparison) =>
        text is not null && text.Contains(search, comparison);
}
