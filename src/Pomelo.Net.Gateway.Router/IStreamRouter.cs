﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace Pomelo.Net.Gateway.Router
{
    public interface IStreamRouter
    {
        Guid Id { get; }
        string Name { get; }
        int ExpectedBufferSize { get; }
        ValueTask<RouteResult> RouteAsync(
            Stream stream,
            Memory<byte> buffer, 
            IPEndPoint listenerEndPoint, 
            IPEndPoint clientEndPoint, 
            CancellationToken cancellationToken = default);
    }
}
