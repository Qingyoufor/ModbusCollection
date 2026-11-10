namespace Client.Models.Dtos
{
    /// <summary>寄存器类型，数值与服务端 Server.Models.Common.RegisterType 保持一致</summary>
    public enum RegisterType : byte
    {
        InputRegister = 3,
        HoldingRegister = 4
    }

    /// <summary>数据类型，数值与服务端 Server.Models.Common.DataPointType 保持一致</summary>
    public enum DataPointType : byte
    {
        Int16 = 0,
        UInt16 = 1,
        Int32 = 2,
        Float = 3
    }

    /// <summary>下拉框选项：值 + 显示文本</summary>
    public record EnumOption<T>(T Value, string Label) where T : struct, System.Enum;
}
