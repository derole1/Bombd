using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Timers;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Logging;
using Timer = System.Timers.Timer;

namespace Bombd.Protocols.TCP;

public class SslConnection : ConnectionBase
{
    private const int MaxMessageSize = 8192;
    private const int KeepAliveFrequency = 3000;
    
    // u32 MsgLength
    // char Md5Digest[16]
    // char Protocol
    // char Source
    // short Destination
    private const int MessageHeaderSize = 24;
    
    private readonly byte[] _recv = new byte[MaxMessageSize];
    private readonly byte[] _send = new byte[MaxMessageSize];

    private readonly object _sendLock = new();
    private Timer? _keepAliveTimer;
    
    private SslStream _sslStream;

    public SslConnection(BombdService service, SslServer server) : base(service, server)
    {
        Id = CryptoHelper.GetRandomSecret();
        State = ConnectionState.WaitingForConnection;
        _server = server;
        _server.Connections.TryAdd(Id, this);
    }

    public int Id { get; }
    private readonly SslServer _server;
    private Socket _socket;
    
    public void Connect(Socket socket)
    {
        _socket = socket;
        try
        {
            _sslStream = new SslStream(new NetworkStream(_socket, false), false);
            _sslStream.AuthenticateAsServer(_server.Certificate, false, SslProtocols.Ssl3, false);
            
            _keepAliveTimer = new Timer(KeepAliveFrequency);
            _keepAliveTimer.Elapsed += OnKeepAliveTimerElapsed;
            _keepAliveTimer.AutoReset = false;
            _keepAliveTimer.Enabled = true;
            
            Task.Run(async () => await Block());
        }
        catch (Exception)
        {
            Logger.LogError<SslConnection>("Handshake failed. Disconnecting.");
            Disconnect();
        }
    }

    public override void Disconnect()
    {
        if (State == ConnectionState.Disconnected) return;

        try
        {
            _keepAliveTimer?.Dispose();

            try
            {
                _sslStream.ShutdownAsync().Wait();
            }
            catch (Exception)
            {
                Logger.LogError<SslConnection>("An error occurred while shutting down SSL stream.");
            }

            _sslStream.Dispose();

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
                Logger.LogError<SslConnection>("An error occurred while shutting down socket.");
            }

            _socket.Close();
            _socket.Dispose();
        }
        catch (ObjectDisposedException)
        {
            
        }
        
        _server.Connections.Remove(Id);
        if (State > ConnectionState.WaitingForConnection)
            Service.OnDisconnected(this);
    }

    private void OnKeepAliveTimerElapsed(object? source, ElapsedEventArgs e)
    {
        Send(ArraySegment<byte>.Empty, PacketType.KeepAlive);
    }

    public override void Send(ArraySegment<byte> data, PacketType type)
    {
        if (State == ConnectionState.Disconnected) return;
        
        int messageSize = MessageHeaderSize + data.Count;
        lock (_sendLock)
        {
            _send[messageSize++] = 0;

            int packetSize = messageSize - 4;
            _send[0] = (byte)((packetSize >> 24) & 0xff);
            _send[1] = (byte)((packetSize >> 16) & 0xff);
            _send[2] = (byte)((packetSize >> 8) & 0xff);
            _send[3] = (byte)(packetSize & 0xff);

            _send[20] = (byte)type;
            _send[21] = 0xFE;
            _send[22] = 0xFF;
            _send[23] = 0xFF;

            Buffer.BlockCopy(data.Array!, data.Offset, _send, MessageHeaderSize, data.Count);
            try
            {
                int offset = 0;
                do
                {
                    int size = Math.Min(1024, messageSize - offset);
                    _sslStream.Write(_send, offset, size);
                    offset += size;
                } while (offset < messageSize);
            }
            catch (Exception)
            {
                Logger.LogError<SslConnection>("An error occurred during send. Closing connection.");
                Disconnect();
            }
        }
    }

    private async Task Block()
    {
        while (State != ConnectionState.Disconnected)
        {
            int payloadSize;
            PacketType type;
            try
            {
                int len = await _sslStream.ReadAsync(_recv.AsMemory(0, MessageHeaderSize));
                if (len != MessageHeaderSize)
                {
                    Logger.LogError<SslConnection>("Received message with invalid header. Closing connection.");
                    Disconnect();
                    return;
                }

                payloadSize = ((_recv[0] << 24) | (_recv[1] << 16) | (_recv[2] << 8) | _recv[3]) -
                              (MessageHeaderSize - 4);
                if (payloadSize is < 0 or > MaxMessageSize)
                {
                    Logger.LogError<SslConnection>("Received message with invalid size. Closing connection.");
                    Disconnect();
                    return;
                }

                type = (PacketType)_recv[20];
                if (payloadSize != 0)
                {
                    int offset = 0;
                    do
                    {
                        len = await _sslStream.ReadAsync(_recv.AsMemory(offset, payloadSize - offset));
                        if (len == 0)
                        {
                            Disconnect();
                            return;
                        }

                        offset += len;
                    } while (offset < payloadSize);
                }
            }
            catch (Exception)
            {
                Logger.LogError<SslConnection>("An error occurred while reading from socket. Closing connection.");
                Disconnect();
                return;
            }

            if (type == PacketType.KeepAlive) _keepAliveTimer!.Start();
            else
            {
                var data = new ArraySegment<byte>(_recv, 0, payloadSize);
                if (State == ConnectionState.WaitingForConnection) HandleStartConnect(data);
                else if (State == ConnectionState.WaitingForTimeSync) HandleTimeSync(data);
                else Service.OnData(this, data, type);
            }   
        }
    }
}