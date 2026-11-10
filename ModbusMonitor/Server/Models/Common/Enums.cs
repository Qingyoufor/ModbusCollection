namespace Server.Models.Common
{


    public enum RegisterType : byte
    {
        InputRegister = 3,
        HoldingRegister = 4
    }

    public enum DataPointType : byte
    {
        Int16 = 0,
        UInt16 = 1,
        Int32 = 2,
        Float = 3
    }

    public enum AlarmType : byte
    {
        HighLimit = 0,
        LowLimit = 1
    }

    public enum AlarmStatus : byte
    {
        Unacknowledged = 0,
        Acknowledged = 1
    }
}
