namespace Server.Models.Dtos
{
    public class RealtimeDeviceDataDto
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string ComPort { get; set; } = string.Empty;
        public List<DataPointValueDto> DataPoints { get; set; } = new();
    }

    public class DataPointValueDto
    {
        public int DataPointId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string? Unit { get; set; }
        public string DataType { get; set; } = string.Empty;
        public double? AlarmHigh { get; set; }
        public double? AlarmLow { get; set; }
        public int DecimalPlaces { get; set; }
        public string AlarmStatus { get; set; } = "Normal"; // Normal / High / Low
    }
}
