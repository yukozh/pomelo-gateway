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
        public static Endpoint ParseRulePacket(Memory<byte> body)
        {
            return new Endpoint
            { 
                Id = Guid.NewGuid(),
                Protocol = (Protocol)body.Span[0],
                RouterId = new Guid(body.Slice(1, 16).Span),
                TunnelId = new Guid(body.Slice(17, 16).Span),
                Port = BitConverter.ToUInt16(body.Slice(33, 2).Span),
                IPAddress = new IPAddress(body.Slice(35).Span),
            };
        }

        public static int BuildRulePacket(Endpoint rule, Memory<byte> buffer)
        {
            buffer.Span[0] = (byte)rule.Protocol;
            rule.RouterId.TryWriteBytes(buffer.Slice(1, 16).Span);
            rule.TunnelId.TryWriteBytes(buffer.Slice(17, 16).Span);
            BitConverter.TryWriteBytes(buffer.Slice(33, 2).Span, rule.Port);
            rule.IPAddress.TryWriteBytes(buffer.Slice(35).Span, out var length);
            return 35 + length;
        }
    }
}
