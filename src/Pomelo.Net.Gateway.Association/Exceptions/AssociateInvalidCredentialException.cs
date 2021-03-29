using System;

namespace Pomelo.Net.Gateway.Association
{
    public class AssociateInvalidCredentialException : Exception
    {
        public AssociateInvalidCredentialException(string message) : base(message) { }
    }
}
