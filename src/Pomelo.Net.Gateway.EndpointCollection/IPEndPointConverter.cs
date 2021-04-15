using System;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class IPEndPointConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(IPEndPoint) || objectType == typeof(IPAddress));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is IPEndPoint || value is IPAddress)
            {
                writer.WriteValue(value.ToString());
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jv = JValue.Load(reader);
            if (IPEndPoint.TryParse(jv.ToString(), out var ep))
            {
                return ep;
            }
            return IPAddress.Parse(jv.ToString());
        }
    }
}
