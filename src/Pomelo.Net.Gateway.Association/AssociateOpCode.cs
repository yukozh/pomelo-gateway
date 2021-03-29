namespace Pomelo.Net.Gateway.Association
{
    // +-----------------+------------------------+------+
    // | OpCode (1 byte) | Packet Length (1 byte) | Body |
    // +-----------------+------------------------+------+

    public enum AssociateOpCode : byte
    {
        BasicAuthLogin  = 0x00,
        SetRule        = 0x01,
        CleanRules      = 0x02
    }
}
