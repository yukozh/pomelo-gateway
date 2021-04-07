namespace Pomelo.Net.Gateway.Tunnel
{
    public enum PacketTunnelOpCode : byte
    {
        Login = 0x00,
        TunnelToAgent = 0xb0,
        AgentToTunnel = 0xa0,
        HeartBeat = 0xff
    }
}
