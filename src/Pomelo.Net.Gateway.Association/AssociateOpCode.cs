namespace Pomelo.Net.Gateway.Association
{
    // +-----------------+------------------------+------+
    // | OpCode (1 byte) | Packet Length (1 byte) | Body |
    // +-----------------+------------------------+------+

    public enum AssociateOpCode : byte
    {
        BasicAuthLogin      = 0x00,
        SetRules            = 0x01
    }
}
