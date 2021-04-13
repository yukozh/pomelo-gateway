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
using Pomelo.Net.Gateway.EndpointManager;
using Pomelo.WebSlotGateway.Models;
using Pomelo.WebSlotGateway.Utils;

namespace Pomelo.WebSlotGateway
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
            services.AddDbContext<SlotContext>(x => x.UseSqlite(Configuration["SQLite"]));
            services.AddPomeloGatewayServer(
                IPEndPoint.Parse("127.0.0.1:16246"),
                IPEndPoint.Parse("127.0.0.1:16247"));
            services.AddControllersWithViews();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.ApplicationServices.RunPomeloGatewayServerAsync().ContinueWith(_ => Task.Run(async () =>
            {
                var tcp = app.ApplicationServices.GetRequiredService<TcpEndpointManager>();
                using (var scope = app.ApplicationServices.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SlotContext>();
                    var rules = await db.Slots
                        .ToListAsync();
                    var serverEndpoint = await app.ApplicationServices.GetRequiredService<ConfigurationHelper>().GetLocalEndpointAsync();
                    foreach (var rule in rules)
                    {
                        await tcp.InsertPreCreateEndpointRuleAsync(
                            rule.Id.ToString(),
                            serverEndpoint,
                            await AddressHelper.ParseAddressAsync(rule.Destination, 0),
                            Guid.Parse("374c20bc-e730-4da3-8c2f-7e570da35268"),
                            Guid.Parse("4048bf29-0997-4f9d-827b-fe29ceb0e4fe"));
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
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
