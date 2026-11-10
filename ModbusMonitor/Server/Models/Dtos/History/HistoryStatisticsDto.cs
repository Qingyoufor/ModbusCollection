namespace Server.Models.Dtos
{
    public class HistoryStatisticsDto
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public int DataPointId { get; set; }
        public string DataPointName { get; set; } = string.Empty;
        public double Max { get; set; }
        public double Min { get; set; }
        public double Average { get; set; }
        public int Count { get; set; }
    }
}
