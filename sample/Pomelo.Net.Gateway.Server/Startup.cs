using System.Linq;
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
using Pomelo.Net.Gateway.Http;
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
            services.AddControllersWithViews().AddRazorRuntimeCompilation();
            services.AddDbContext<ServerContext>(x => x.UseSqlite(Configuration["DB"]));
            services.AddPomeloGatewayServer()
                .AddPomeloHttpStack();
            services.AddSingleton<IAuthenticator, DbBasicAuthenticator>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            using var scope = app.ApplicationServices.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<ServerContext>();
            db.InitDatabaseAsync().GetAwaiter().GetResult();
            app.ApplicationServices.RunPomeloGatewayServer(IPEndPoint.Parse(Configuration["AssociationServer"]), IPEndPoint.Parse(Configuration["TunnelServer"]));

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
