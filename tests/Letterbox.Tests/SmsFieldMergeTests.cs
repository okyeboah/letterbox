using Letterbox.Sms;

namespace Letterbox.Tests;

public class SmsFieldMergeTests
{
    [Fact]
    public void JsonFieldsAreCaptured()
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SmsCaptureEndpoint.MergeJsonFields(fields, """{"to": "+233201234567", "message": "hi", "count": 3, "flag": true}""");
        Assert.Equal("+233201234567", fields["to"]);
        Assert.Equal("hi", fields["message"]);
        Assert.Equal("3", fields["count"]);
        Assert.Equal("true", fields["flag"]);
    }

    [Fact]
    public void MalformedJsonKeepsFieldsEmpty() {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SmsCaptureEndpoint.MergeJsonFields(fields, "{not json");
        Assert.Empty(fields);
    }

    [Fact]
    public void FormFieldsAreDecoded()
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SmsCaptureEndpoint.MergeFormFields(fields, "to=%2B233201234567&message=a+b+c&empty");
        Assert.Equal("+233201234567", fields["to"]);
        Assert.Equal("a b c", fields["message"]);
        Assert.Equal("", fields["empty"]);
    }
}
