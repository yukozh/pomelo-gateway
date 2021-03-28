﻿using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Tunnel
{
    public interface IPacketTunnel
    {
        Guid Id { get; }
        string Name { get; }
        ValueTask ForwardAsync(Socket leftToTunnel, Socket tunnelToRight, CancellationToken cancellationToken = default);
        ValueTask BackwardAsync(Socket rightToTunnel, Socket tunnelToLeft, CancellationToken cancellationToken = default);
    }
}
