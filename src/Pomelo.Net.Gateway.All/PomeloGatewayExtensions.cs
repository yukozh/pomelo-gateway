using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Pomelo.Net.Gateway
{
    public static class PomeloGatewayExtensions
    {
        public static IServiceCollection AddPomeloGatewayClient(
            this IServiceCollection services,
            IPEndPoint associateServerEndpoint,
            IPEndPoint tunnelServerEndpoint,
            string ruleJsonPath = "rules.json")
        {
            return services.AddLogging()
                .AddSingleton(services
                    => new Association.AssociateClient(associateServerEndpoint, tunnelServerEndpoint, services))
                .AddSingleton<Tunnel.StreamTunnelContextFactory>()
                .AddSingleton<Tunnel.PacketTunnelContextFactory>()
                .AddSingleton<EndpointCollection.IMappingRuleProvider, EndpointCollection.LocalFileMappingRuleProvider>(services
                    => new EndpointCollection.LocalFileMappingRuleProvider(ruleJsonPath))
                .AddSingleton<Association.Authentication.IAuthenticator, Association.Authentication.DefaultBasicAuthenticator>()
                .AddSingleton<Tunnel.IStreamTunnel, Tunnel.DefaultStreamTunnel>()
                .AddSingleton<Tunnel.IPacketTunnel, Tunnel.AgentSidePacketTunnel>()
                .AddSingleton<Association.Token.ITokenProvider>(services
                    => services.GetRequiredService<Association.AssociateClient>());
        }

        public static IServiceCollection AddPomeloGatewayServer(
            this IServiceCollection services,
            IPEndPoint associateServerEndpoint,
            IPEndPoint tunnelServerEndpoint)
        {
            return services.AddLogging()
                .AddPomeloGatewayEndpointCollection()
                .AddSingleton(services
                    => new Association.AssociateServer(associateServerEndpoint, services))
                .AddSingleton<Association.Authentication.IAuthenticator, Association.Authentication.DefaultBasicAuthenticator>()
                .AddSingleton<Tunnel.StreamTunnelContextFactory>()
                .AddSingleton<Tunnel.PacketTunnelContextFactory>()
                .AddSingleton(services 
                    => new Tunnel.StreamTunnelServer(tunnelServerEndpoint, services))
                .AddSingleton(services
                    => new Tunnel.PacketTunnelServer(tunnelServerEndpoint, services))
                .AddSingleton<Association.Token.ITokenValidator>(services 
                    => services.GetRequiredService<Association.AssociateServer>())
                .AddSingleton<Tunnel.ITunnelCreationNotifier>(services 
                    => services.GetRequiredService<Association.AssociateServer>())
                .AddSingleton<Association.Udp.IUdpAssociator>(services
                    => services.GetRequiredService<Association.AssociateServer>())
                .AddSingleton<EndpointManager.TcpEndpointManager>()
                .AddSingleton<EndpointManager.UdpEndpointManager>()
                .AddSingleton<Tunnel.IStreamTunnel, Tunnel.DefaultStreamTunnel>()
                .AddSingleton<Tunnel.IPacketTunnel, Tunnel.ServerSidePacketTunnel>()
                .AddSingleton<Tunnel.IPacketTunnel, Tunnel.PublicPacketTunnel>()
                .AddSingleton<Router.IStreamRouter, Router.DefaultStreamRouter>()
                .AddSingleton<Router.IPacketRouter, Router.DefaultPacketRouter>();
        }

        public static Task RunPomeloGatewayServerAsync(this IServiceProvider services)
        {
            return Task.Factory.StartNew(() =>
            {
                services.GetRequiredService<Association.AssociateServer>().Start();
                services.GetRequiredService<Tunnel.StreamTunnelServer>().Start();
                services.GetRequiredService<Tunnel.PacketTunnelServer>().Start();
            }, TaskCreationOptions.LongRunning);
        }

        public static void RunPomeloGatewayClient(this IServiceProvider services)
        {
            Task.Factory.StartNew(() =>
            {
                services.GetRequiredService<Association.AssociateClient>().Start();
            }, TaskCreationOptions.LongRunning);
        }
    }
}
