using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class FileBasedStaticRuleProvider : IStaticRuleProvider
    {
        private string rulesFilePath;

        public FileBasedStaticRuleProvider(string rulesFilePath = "gateway-static-rules.json")
        { 
            this.rulesFilePath = rulesFilePath;
        }

        public async ValueTask<StaticRule> GetStaticRuleByListenerEndPointAsync(
            Protocol protocol, 
            IPEndPoint endPoint, 
            CancellationToken cancellationToken = default)
        {
            var rules = await GetStaticRulesAsync(cancellationToken);
            return rules.FirstOrDefault(x => x.Protocol == protocol && x.ListenerEndpoint.Equals(endPoint));
        }

        public async ValueTask<IEnumerable<StaticRule>> GetStaticRulesAsync(
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(rulesFilePath))
            { 
                return Enumerable.Empty<StaticRule>();
            }

            var text = await File.ReadAllTextAsync(rulesFilePath, cancellationToken);
            return JsonConvert.DeserializeObject<IEnumerable<StaticRule>>(text);
        }
    }

    public static class FileBasedStaticRuleProviderExtensions
    {
        public static IServiceCollection AddFileBasedStaticRuleProvider(
            this IServiceCollection services,
            string rulesFilePath = "gateway-static-rules.json")
            => services.AddSingleton<IStaticRuleProvider>(x => new FileBasedStaticRuleProvider(rulesFilePath));
    }
}
