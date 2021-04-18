using System.Collections.Generic;
using System.Web;

namespace Pomelo.Net.Gateway.Http
{
    public class UrlEncodedValueCollection
    {
        private Dictionary<string, HttpValue> fields = new Dictionary<string, HttpValue>();
        public Dictionary<string, HttpValue> Fields => fields;

        public UrlEncodedValueCollection()
        {
        }

        public UrlEncodedValueCollection(string value)
        {
            LoadFromString(value);
        }

        public void LoadFromString(string value)
        {
            var splitedFields = value.Split('&');
            foreach (var splitedField in splitedFields)
            {
                var index = splitedField.IndexOf('=');
                var key = HttpUtility.UrlDecode(splitedField.Substring(0, index));
                var val = HttpUtility.UrlDecode(splitedField.Substring(index + 1));
                if (!fields.ContainsKey(key))
                {
                    fields.Add(key, new HttpValue());
                }
                fields[key].Add(val);
            }
        }
    }
}
