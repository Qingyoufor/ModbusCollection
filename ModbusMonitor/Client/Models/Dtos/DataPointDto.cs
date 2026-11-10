namespace Client.Models.Dtos
{
    /// <summary>服务端返回的测点（RegisterType/DataType 是枚举名字符串）</summary>
    public class DataPointDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int RegisterAddress { get; set; }
        public string RegisterType { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public string? Unit { get; set; }
        public double? AlarmHigh { get; set; }
        public double? AlarmLow { get; set; }
        public double ScaleFactor { get; set; }
        public int DecimalPlaces { get; set; }
        public int SortOrder { get; set; }
    }
}
