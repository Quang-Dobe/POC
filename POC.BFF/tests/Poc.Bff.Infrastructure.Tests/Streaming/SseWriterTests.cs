namespace Poc.Bff.Infrastructure.Tests.Streaming;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Poc.Bff.Infrastructure.Streaming;
using Xunit;

public class SseWriterTests
{
    private static async Task<string> CaptureAsync(Func<SseWriter, Task> write)
    {
        using var body = new MemoryStream();
        var writer = new SseWriter(body);

        await write(writer);

        return Encoding.UTF8.GetString(body.ToArray());
    }

    [Fact]
    public async Task WriteMessageAsync_singleLineChunk_emitsExactlyOneDataLineThenBlankLine()
    {
        var frame = await CaptureAsync(w => w.WriteMessageAsync("hello world"));

        Assert.Equal("data: hello world\n\n", frame);
    }

    [Fact]
    public async Task WriteMessageAsync_multiLineChunk_emitsOneDataLinePerSegmentThenBlankLine()
    {
        var frame = await CaptureAsync(w => w.WriteMessageAsync("line one\nline two\nline three"));

        Assert.Equal("data: line one\ndata: line two\ndata: line three\n\n", frame);
    }

    [Fact]
    public async Task WriteMessageAsync_emptyChunk_emitsASingleEmptyDataLineThenBlankLine()
    {
        var frame = await CaptureAsync(w => w.WriteMessageAsync(string.Empty));

        Assert.Equal("data: \n\n", frame);
    }

    [Fact]
    public async Task WriteMessageAsync_writesNoEventLine_soMessageStaysTheSseDefaultEvent()
    {
        var frame = await CaptureAsync(w => w.WriteMessageAsync("chunk"));

        Assert.DoesNotContain("event:", frame);
    }

    [Fact]
    public async Task WriteMessageAsync_usesBareLineFeeds_neverCarriageReturns()
    {
        var frame = await CaptureAsync(w => w.WriteMessageAsync("a\nb"));

        Assert.DoesNotContain('\r', frame);
    }

    [Fact]
    public void Constructor_rejectsANullBody()
    {
        Assert.Throws<ArgumentNullException>(() => new SseWriter(null!));
    }

    [Fact]
    public async Task WriteMessageAsync_rejectsANullChunk()
    {
        using var body = new MemoryStream();
        var writer = new SseWriter(body);

        await Assert.ThrowsAsync<ArgumentNullException>(() => writer.WriteMessageAsync(null!));
    }
}
