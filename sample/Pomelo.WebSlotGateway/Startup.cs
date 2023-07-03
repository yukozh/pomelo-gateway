using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pomelo.Net.Gateway;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.EndpointManager;
using Pomelo.Net.Gateway.Http;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;
using Pomelo.WebSlotGateway.Models;
using Pomelo.WebSlotGateway.Utils;

namespace Pomelo.WebSlotGateway
{
    public class Startup
    {
        public static Guid RouterId = Guid.Parse("374c20bc-e730-4da3-8c2f-7e570da35268");
        public static Guid TunnelId = Guid.Parse("5237e1e5-5f1a-4df0-a716-2f3dbac2a3ff");

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<GatewayContext>(x => x.UseSqlite(Configuration["SQLite"]));
            services.AddSingleton<ConfigurationHelper>();
            services.AddSingleton<HealthCheckerProcesser>();
            services.AddSingleton<IStreamRouter, ARRAffinityRouter>();
            services.AddSingleton<IStreamTunnel, HttpTunnel>();
            services.AddSingleton<IHttpInterceptor, DefaultHttpInterceptor>();
            services.AddSingleton<IHttpInterceptor, GoogleInterceptor>();
            services.AddSingleton<IHttpInterceptor, HeaderInterceptor>();
            services.AddSingleton<IHttpInterceptor, HttpHeaderInterceptor>();
            services.AddSingleton<IHealthChecker, DefaultHealthChecker>();
            services.AddPomeloGatewayServer();
            services.AddControllersWithViews().AddNewtonsoftJson(x =>
            {
                x.SerializerSettings.Converters.Add(new IPEndPointConverter());
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GatewayContext>();
                db.InitDatabaseAsync().GetAwaiter().GetResult();
            }
                
            app.ApplicationServices.RunPomeloGatewayServerAsync(
                IPEndPoint.Parse("127.0.0.1:16246"),
                IPEndPoint.Parse("127.0.0.1:16247"))
                .ContinueWith(t => Task.Run(async () =>
                {
                    var tcp = app.ApplicationServices.GetRequiredService<TcpEndpointManager>();
                    using (var scope = app.ApplicationServices.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<GatewayContext>();
                        var rules = await db.Slots
                            .ToListAsync();
                        var serverEndpoint = await app.ApplicationServices.GetRequiredService<ConfigurationHelper>().GetLocalEndpointAsync();
                        foreach (var rule in rules)
                        {
                            await tcp.InsertPreCreateEndpointRuleAsync(
                                rule.Id.ToString(),
                                serverEndpoint,
                                rule.Destination,
                                RouterId,
                                TunnelId,
                                rule.DestinationType == DestinationType.Https);
                        }
                    }
                    _ = tcp.EnsurePreCreateEndpointsAsync();
                }));
            app.ApplicationServices.GetRequiredService<HealthCheckerProcesser>();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

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
