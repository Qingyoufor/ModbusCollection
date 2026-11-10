namespace Server.Models.Dtos
{
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
    }
}
