using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Association;

namespace Pomelo.Net.Gateway
{
    public static class PomeloGatewayExtensions
    {
        public static IServiceCollection AddPomeloGatewayClient(
            this IServiceCollection services,
            string ruleJsonPath = "gateway-client-rules.json")
        {
            return services.AddLogging()
                .AddSingleton(services
                    => new Association.AssociateClient(services))
                .AddSingleton(services
                    => new Tunnel.PacketTunnelClient(services))
                .AddSingleton<Tunnel.StreamTunnelContextFactory>()
                .AddSingleton<Tunnel.PacketTunnelContextFactory>()
                .AddSingleton<EndpointCollection.IMappingRuleProvider, EndpointCollection.LocalFileMappingRuleProvider>(services
                    => new EndpointCollection.LocalFileMappingRuleProvider(ruleJsonPath))
                .AddSingleton<Association.Authentication.IAuthenticator, Association.Authentication.DefaultBasicAuthenticator>()
                .AddSingleton<Tunnel.IStreamTunnel, Tunnel.DefaultStreamTunnel>()
                .AddSingleton<Tunnel.IPacketTunnel, Tunnel.AgentSidePacketTunnel>()
                .AddSingleton<Association.Token.ITokenProvider>(services
                    => services.GetRequiredService<Association.AssociateClient>())
                .AddSingleton<Tunnel.IPacketTunnelServerAddressProvider>(services
                    => services.GetRequiredService<Association.AssociateClient>());
        }

        public static IServiceCollection AddPomeloGatewayServer(
            this IServiceCollection services)
        {
            return services.AddLogging()
                .AddPomeloGatewayEndpointCollection()
                .AddSingleton<AssociateServer>()
                .AddSingleton<Association.Authentication.IAuthenticator, Association.Authentication.DefaultBasicAuthenticator>()
                .AddSingleton<Tunnel.StreamTunnelContextFactory>()
                .AddSingleton<Tunnel.PacketTunnelContextFactory>()
                .AddSingleton(services 
                    => new Tunnel.StreamTunnelServer(services))
                .AddSingleton(services
                    => new Tunnel.PacketTunnelServer(services))
                .AddSingleton<Association.Token.ITokenValidator>(services 
                    => services.GetRequiredService<Association.AssociateServer>())
                .AddSingleton<Tunnel.ITunnelCreationNotifier>(services 
                    => services.GetRequiredService<Association.AssociateServer>())
                .AddSingleton<Association.Udp.IUdpAssociator>(services
                    => services.GetRequiredService<Association.AssociateServer>())
                .AddSingleton<Tunnel.IUdpEndpointListenerFinder>(services
                    => services.GetRequiredService<EndpointManager.UdpEndpointManager>())
                .AddSingleton<EndpointManager.TcpEndpointManager>()
                .AddSingleton<EndpointManager.UdpEndpointManager>()
                .AddSingleton<Tunnel.IStreamTunnel, Tunnel.DefaultStreamTunnel>()
                .AddSingleton<Tunnel.IPacketTunnel, Tunnel.ServerSidePacketTunnel>()
                .AddSingleton<Tunnel.IPacketTunnel, Tunnel.PublicPacketTunnel>()
                .AddSingleton<Router.IStreamRouter, Router.DefaultStreamRouter>()
                .AddSingleton<Router.IPacketRouter, Router.DefaultPacketRouter>();
        }

        public static Task RunPomeloGatewayServerAsync(this IServiceProvider services, IPEndPoint associateServerEndpoint, IPEndPoint tunnelServerEndpoint)
        {
            return Task.Factory.StartNew(() =>
            {
                services.GetRequiredService<AssociateServer>().Start(associateServerEndpoint);
                services.GetRequiredService<Tunnel.StreamTunnelServer>().Start(tunnelServerEndpoint);
                services.GetRequiredService<Tunnel.PacketTunnelServer>().Start(tunnelServerEndpoint);
            }, TaskCreationOptions.LongRunning);
        }

        public static void RunPomeloGatewayClient(
            this IServiceProvider services,
            IPEndPoint associateServerEndpoint, 
            IPEndPoint tunnelServerEndpoint)
        {
            Task.Factory.StartNew(() =>
            {
                var client = services.GetRequiredService<Association.AssociateClient>();
                client.SetServers(associateServerEndpoint, tunnelServerEndpoint);
                services.GetRequiredService<Association.AssociateClient>().Start();
                var _ = services.GetRequiredService<Tunnel.PacketTunnelClient>();
            }, TaskCreationOptions.LongRunning);
        }

        public static IServiceCollection AddPomeloHttpStack(this IServiceCollection services)
        {
            return services.AddSingleton<Tunnel.IStreamTunnel, Http.HttpTunnel>()
                .AddSingleton<Http.IHttpInterceptor, Http.DefaultHttpInterceptor>();
        }
    }
}
