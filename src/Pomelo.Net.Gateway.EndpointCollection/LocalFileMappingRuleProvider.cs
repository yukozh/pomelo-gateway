using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class LocalFileMappingRuleProvider : IMappingRuleProvider
    {
        private List<MappingRule> rules;
        private string path;
        private JsonSerializerSettings settings;

        public IReadOnlyCollection<MappingRule> Rules => rules;

        public LocalFileMappingRuleProvider(string path)
        {
            this.rules = new List<MappingRule>();
            this.path = path;
            this.settings = new JsonSerializerSettings();
            this.settings.Converters.Add(new IPEndPointConverter());
            Reload();
        }

        public void Reload()
        {
            rules.Clear();
            var jsonText = File.ReadAllText(path);
            rules.AddRange(JsonConvert.DeserializeObject<IEnumerable<MappingRule>>(jsonText, settings));
        }

        public async ValueTask SetRulesAsync(
            IEnumerable<MappingRule> rules, 
            CancellationToken cancellationToken = default)
        {
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(rules, settings));
        }
    }
}
