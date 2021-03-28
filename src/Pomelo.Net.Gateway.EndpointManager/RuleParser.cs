using System;
using System.Net;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public static class RuleParser
    {
        // +-------------------+----------------+----------------------------------------+
        // | Protocol (1 byte) | Port (2 bytes) | Address (IPv4 4 bytes / IPv6 16 bytes) |
        // +-------------------+----------------+----------------------------------------+
        public static Endpoint ParseRulePacket(Memory<byte> body)
        {
            return new Endpoint
            { 
                Id = Guid.NewGuid(),
                Protocol = (Protocol)body.Span[0],
                Port = BitConverter.ToUInt16(body.Slice(1, 2).Span),
                IPAddress = new IPAddress(body.Slice(3).Span)
            };
        }

        public static int BuildRulePacket(Endpoint rule, Memory<byte> buffer)
        {
            buffer.Span[0] = (byte)rule.Protocol;
            BitConverter.TryWriteBytes(buffer.Slice(1, 2).Span, rule.Port);
            rule.IPAddress.TryWriteBytes(buffer.Slice(3).Span, out var length);
            return 3 + length;
        }
    }
}
