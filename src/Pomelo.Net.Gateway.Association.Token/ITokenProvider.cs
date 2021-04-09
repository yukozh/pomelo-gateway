namespace Pomelo.Net.Gateway.Association.Token
{
    public interface ITokenProvider
    {
        long Token { get; }
        string UserIdentifier { get; }
    }
}
