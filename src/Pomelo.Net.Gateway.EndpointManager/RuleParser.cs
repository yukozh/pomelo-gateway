using System;
using System.Net;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public static class RuleParser
    {
        // +-------------------+----------------------+----------------------+----------------+----------------------------------------+
        // | Protocol (1 byte) | Router Id (16 bytes) | Tunnel Id (16 bytes) | Port (2 bytes) | Address (IPv4 4 bytes / IPv6 16 bytes) |
        // +-------------------+----------------------+----------------------+----------------+----------------------------------------+
        public static EndpointCollection.EndPoint ParseRulePacket(Memory<byte> body)
        {
            return new EndpointCollection.EndPoint
            { 
                Id = Guid.NewGuid(),
                Protocol = (Protocol)body.Span[0],
                RouterId = new Guid(body.Slice(1, 16).Span),
                TunnelId = new Guid(body.Slice(17, 16).Span),
                ListenerEndPoint = new IPEndPoint(new IPAddress(body.Slice(35).Span), BitConverter.ToUInt16(body.Slice(33, 2).Span))
            };
        }

        public static int BuildRulePacket(EndpointCollection.EndPoint rule, Memory<byte> buffer)
        {
            buffer.Span[0] = (byte)rule.Protocol;
            rule.RouterId.TryWriteBytes(buffer.Slice(1, 16).Span);
            rule.TunnelId.TryWriteBytes(buffer.Slice(17, 16).Span);
            BitConverter.TryWriteBytes(buffer.Slice(33, 2).Span, (ushort)rule.ListenerEndPoint.Port);
            rule.ListenerEndPoint.Address.TryWriteBytes(buffer.Slice(35).Span, out var length);
            return 35 + length;
        }
    }
}
