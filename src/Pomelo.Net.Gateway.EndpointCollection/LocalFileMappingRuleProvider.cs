using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class LocalFileMappingRuleProvider : IMappingRuleProvider
    {
        private List<MappingRule> rules;
        private string path;
        public IReadOnlyCollection<MappingRule> Rules => rules;

        public LocalFileMappingRuleProvider(string path)
        {
            this.rules = new List<MappingRule>();
            this.path = path;
            Reload();
        }

        public void Reload()
        {
            rules.Clear();
            var jsonText = File.ReadAllText(path);
            rules.AddRange(JsonConvert.DeserializeObject<IEnumerable<MappingRule>>(jsonText));
        }
    }
}
