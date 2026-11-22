using System.Net;
using System.Net.Sockets;

namespace WorshipStudio.Tests;

internal sealed class DisposableTcpListener : TcpListener, IDisposable
{
    public DisposableTcpListener(IPAddress address, int port) : base(address, port) { }
    void IDisposable.Dispose() => Stop();
}

#if NET6_0
internal static class LegacyTestStreamExtensions
{
    public static async Task ReadExactlyAsync(this Stream stream, Memory<byte> buffer, CancellationToken token)
    {
        while (!buffer.IsEmpty)
        {
            var count = await stream.ReadAsync(buffer, token);
            if (count == 0) throw new EndOfStreamException();
            buffer = buffer[count..];
        }
    }
}
#endif
