using System;

namespace Pomelo.Net.Gateway.Association
{
    public class AssociateClientConflictException : Exception
    {
        public AssociateClientConflictException(string message) : base(message) { }
    }
}
