using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pomelo.Net.Gateway.Association.Authentication;
using Pomelo.Net.Gateway.EndpointManager;
using Pomelo.Net.Gateway.Server.Authentication;
using Pomelo.Net.Gateway.Server.Authenticator;
using Pomelo.Net.Gateway.Server.Models;

namespace Pomelo.Net.Gateway.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication("BasicAuthentication")
                .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
            services.AddControllersWithViews();
            services.AddDbContext<ServerContext>(x => x.UseSqlite(Configuration["DB"]));
            services.AddPomeloGatewayServer(
                IPEndPoint.Parse(Configuration["AssociationServer"]), 
                IPEndPoint.Parse(Configuration["TunnelServer"]));
            services.AddSingleton<IAuthenticator, DbBasicAuthenticator>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.ApplicationServices.RunPomeloGatewayServerAsync().ContinueWith((_)=> Task.Run(async ()=>
            {
                var tcp = app.ApplicationServices.GetRequiredService<TcpEndpointManager>();
                using (var scope = app.ApplicationServices.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ServerContext>();
                    var rules = await db.PublicRules.ToListAsync();
                    foreach (var rule in rules)
                    {
                        if (rule.Protocol == EndpointCollection.Protocol.TCP)
                        {
                            await tcp.InsertPreCreateEndpointRuleAsync(
                                rule.Id,
                                IPEndPoint.Parse(rule.ServerEndpoint),
                                await AddressHelper.ParseAddressAsync(rule.DestinationEndpoint, 0),
                                rule.RouterId,
                                rule.TunnelId);
                        }
                    }
                }
                tcp.EnsurePreCreateEndpointsAsync();
            }));

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
