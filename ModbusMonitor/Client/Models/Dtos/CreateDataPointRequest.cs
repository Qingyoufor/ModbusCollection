namespace Client.Models.Dtos
{
    /// <summary>新增测点请求（枚举按数值序列化，与服务端一致）</summary>
    public class CreateDataPointRequest
    {
        public string Name { get; set; } = string.Empty;
        public int RegisterAddress { get; set; }
        public RegisterType RegisterType { get; set; } = RegisterType.HoldingRegister;
        public DataPointType DataType { get; set; } = DataPointType.Int16;
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public string? Unit { get; set; }
        public double? AlarmHigh { get; set; }
        public double? AlarmLow { get; set; }
        public double ScaleFactor { get; set; } = 1.0;
        public int DecimalPlaces { get; set; }
        public int SortOrder { get; set; }
    }
}
