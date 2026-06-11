namespace Poc.Bff.Infrastructure.Streaming;

using System.Text;

public sealed class SseWriter
{
    private const string DataField = "data: ";
    private const string LineEnd = "\n";

    private readonly Stream _body;

    public SseWriter(Stream body)
    {
        ArgumentNullException.ThrowIfNull(body);
        _body = body;
    }

    public async Task WriteMessageAsync(string chunk, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        var frame = new StringBuilder();
        foreach (var segment in chunk.Split('\n'))
        {
            frame.Append(DataField).Append(segment).Append(LineEnd);
        }

        frame.Append(LineEnd);
        await WriteFrameAsync(frame.ToString(), ct).ConfigureAwait(false);
    }

    private async Task WriteFrameAsync(string frame, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(frame);
        await _body.WriteAsync(bytes, ct).ConfigureAwait(false);
        await _body.FlushAsync(ct).ConfigureAwait(false);
    }
}
