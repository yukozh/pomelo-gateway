namespace Pomelo.Net.Gateway.Association
{
    // +-----------------+------------------------+------+
    // | OpCode (1 byte) | Packet Length (1 byte) | Body |
    // +-----------------+------------------------+------+

    public enum AssociateOpCode : byte
    {
        Version             = 0x00,
        BasicAuthLogin      = 0x01,
        ListStreamRouters   = 0x02,
        ListPacketRouters   = 0x03,
        ListStreamTunnels   = 0x04,
        ListPacketTUnnels   = 0x05,
        SetRules            = 0x06,
        CleanUpRules        = 0x07
    }
}
