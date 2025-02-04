using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MessagePipe.Interprocess.Workers
{
    // TODO:TCP STREAM AND READ

    internal sealed class SocketTcpServer : IDisposable
    {
        const int MaxConnections = 0x7fffffff;

        readonly Socket socket;

        SocketTcpServer(AddressFamily addressFamily)
        {
            socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public static SocketTcpServer Listen(string host, int port)
        {
            var ip = new IPEndPoint(IPAddress.Parse(host), port);
            var server = new SocketTcpServer(ip.AddressFamily);

            server.socket.Bind(ip);
            server.socket.Listen(MaxConnections);
            return server;
        }

        public async void StartAcceptLoopAsync(Action<SocketTcpClient> onAccept, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Socket remote = default;
                try
                {
                    remote = await socket.AcceptAsync();
                }
                catch // (ObjectDisposedException)
                {
                    return;
                }
                onAccept(new SocketTcpClient(remote));
            }
        }

        public void Dispose()
        {
            socket.Dispose();
        }
    }

    internal sealed class SocketTcpClient : IDisposable
    {
        readonly Socket socket;

        SocketTcpClient(AddressFamily addressFamily)
        {
            socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        internal SocketTcpClient(Socket socket)
        {
            this.socket = socket;
        }

        public static SocketTcpClient Connect(string host, int port)
        {
            var ip = new IPEndPoint(IPAddress.Parse(host), port);
            var client = new SocketTcpClient(ip.AddressFamily);
            client.socket.Connect(ip);
            return client;
        }

        public async UniTask<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
#if NET5_0_OR_GREATER
            var xs = new ArraySegment<byte>(buffer, offset, count);
            var i = await socket.ReceiveAsync(xs, SocketFlags.None, cancellationToken).ConfigureAwait(false);
            return i;
#else
            var tcs = new UniTaskCompletionSource<int>();

            socket.BeginReceive(buffer, offset, count, SocketFlags.None, x =>
            {
                int i;
                try
                {
                    i = socket.EndReceive(x);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    return;
                }
                tcs.TrySetResult(i);
            }, null);

            return await tcs.Task;
#endif
        }

        public UniTask<int> SendAsync(byte[] buffer, CancellationToken cancellationToken = default)
        {
#if NET5_0_OR_GREATER
            return socket.SendAsync(buffer, SocketFlags.None, cancellationToken);
#else
            var tcs = new UniTaskCompletionSource<int>();
            socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, x =>
             {
                 int i;
                 try
                 {
                     i = socket.EndSend(x);
                 }
                 catch (Exception ex)
                 {
                     tcs.TrySetException(ex);
                     return;
                 }
                 tcs.TrySetResult(i);
             }, null);

#if !UNITY_2018_3_OR_NEWER
            return new UniTask<int>(tcs.Task);
#else
            return tcs.Task;
#endif
#endif
        }

        public void Dispose()
        {
            socket.Dispose();
        }
    }
}