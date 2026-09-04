using Letterbox.Captures;

namespace Letterbox.Tests;

public class CaptureStoreTests
{
    static Capture Make(string id, Channel channel = Channel.Sms, string? subject = null, string message = "") =>
        new()
        {
            Id = id,
            Channel = channel,
            ReceivedAt = DateTimeOffset.Now,
            Subject = subject,
            RawBody = message,
            PlainBody = channel == Channel.Email ? message : null,
        };

    [Fact]
    public void GetReturnsAddedCapture()
    {
        var store = new CaptureStore(10);
        store.Add(Make("1"));
        Assert.NotNull(store.Get("1"));
        Assert.Null(store.Get("2"));
    }

    [Fact]
    public void OldestCaptureIsEvictedAtCapacity()
    {
        var store = new CaptureStore(2);
        store.Add(Make("1"));
        store.Add(Make("2"));
        store.Add(Make("3"));
        Assert.Null(store.Get("1"));
        Assert.NotNull(store.Get("2"));
        Assert.NotNull(store.Get("3"));
    }

    [Fact]
    public void ListsNewestFirstAndFiltersByChannel()
    {
        var store = new CaptureStore(10);
        store.Add(Make("1", Channel.Sms));
        store.Add(Make("2", Channel.Email));
        store.Add(Make("3", Channel.Sms));

        var all = store.List(null, null, 100);
        Assert.Equal(["3", "2", "1"], all.Select(capture => capture.Id));

        var sms = store.List(Channel.Sms, null, 100);
        Assert.Equal(["3", "1"], sms.Select(capture => capture.Id));
    }

    [Fact]
    public void SearchMatchesSubject()
    {
        var store = new CaptureStore(10);
        store.Add(Make("1", subject: "Invoice attached"));
        store.Add(Make("2", subject: "Report"));
        Assert.Single(store.List(null, "invoice", 100));
    }

    [Fact]
    public void DeleteRemovesOneCapture()
    {
        var store = new CaptureStore(10);
        store.Add(Make("1"));
        Assert.True(store.Delete("1"));
        Assert.False(store.Delete("1"));
        Assert.Null(store.Get("1"));
    }

    [Fact]
    public void ClearReturnsRemovedCount()
    {
        var store = new CaptureStore(10);
        store.Add(Make("1"));
        store.Add(Make("2"));
        Assert.Equal(2, store.Clear());
        Assert.Empty(store.List(null, null, 100));
    }

    [Fact]
    public void NextIdIncrements()
    {
        var store = new CaptureStore(10);
        Assert.Equal("1", store.NextId());
        Assert.Equal("2", store.NextId());
    }
}
