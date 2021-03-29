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
                .AddSingleton<EndpointCollection.IMappingRuleProvider, EndpointCollection.LocalFileMappingRuleProvider>(services
                    => new EndpointCollection.LocalFileMappingRuleProvider(ruleJsonPath))
                .AddSingleton<Association.Authentication.IAuthenticator, Association.Authentication.DefaultBasicAuthenticator>()
                .AddSingleton<Tunnel.IStreamTunnel, Tunnel.DefaultStreamTunnel>();
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
                .AddSingleton(services 
                    => new Tunnel.StreamTunnelServer(tunnelServerEndpoint, services))
                .AddSingleton<Association.Token.ITokenValidator>(services 
                    => services.GetRequiredService<Association.AssociateServer>())
                .AddSingleton<Tunnel.ITunnelCreationNotifier>(services 
                    => services.GetRequiredService<Association.AssociateServer>())
                .AddSingleton<EndpointManager.TcpEndpointManager>()
                .AddSingleton<Tunnel.IStreamTunnel, Tunnel.DefaultStreamTunnel>()
                .AddSingleton<Router.IStreamRouter, Router.DefaultStreamRouter>();
        }

        public static void RunPomeloGatewayServer(this IServiceProvider services)
        {
            services.GetRequiredService<Association.AssociateServer>().Start();
            services.GetRequiredService<Tunnel.StreamTunnelServer>().Start();
        }

        public static void RunPomeloGatewayClient(this IServiceProvider services)
        {
            services.GetRequiredService<Association.AssociateClient>();
        }
    }
}
