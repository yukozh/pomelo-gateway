using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Pomelo.Net.Gateway.Server.Utils
{
    public class VueMiddleware
    {
        private readonly RequestDelegate _next;

        public VueMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var env = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var path = Path.Combine(env.WebRootPath, httpContext.Request.Path.ToString().Split('?')[0].Trim('/'));
            if (File.Exists(path + ".html") || File.Exists(path + "/index.html"))
            {
                httpContext.Response.StatusCode = 200;
                httpContext.Response.ContentType = "text/html";
                if (File.Exists(path + ".html") && path.Substring(env.WebRootPath.Length).Trim('/').IndexOf('/') == -1)
                {
                    await httpContext.Response.WriteAsync(File.ReadAllText(path + ".html"));
                }
                else
                {
                    await httpContext.Response.WriteAsync(File.ReadAllText(Path.Combine(env.WebRootPath, "index.html")));
                }
                await httpContext.Response.CompleteAsync();
                httpContext.Response.Body.Close();
                return;
            }
            await _next(httpContext);
        }
    }

    public static class VueMiddlewareExtensions
    {
        public static IApplicationBuilder UseVueMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<VueMiddleware>();
        }
    }
}
