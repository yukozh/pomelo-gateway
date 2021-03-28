using System.Net;

namespace Pomelo.Net.Gateway.Association.Authentication
{
    public struct Credential
    {
        public bool IsSucceeded;
        public string Identifier;
        public long Token;
    }
}
