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
        SetRules            = 0x04,
        CleanUpRules        = 0x05
    }
}
