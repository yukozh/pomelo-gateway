using System.Collections.Generic;

namespace Pomelo.Net.Gateway.Http
{
    public class HttpValue : List<string>
    {
        public string Value => string.Join(", ", this);
    }
}
