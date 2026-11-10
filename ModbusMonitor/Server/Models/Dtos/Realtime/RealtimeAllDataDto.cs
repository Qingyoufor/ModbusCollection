namespace Server.Models.Dtos
{
    public class RealtimeAllDataDto
    {
        public List<RealtimeDeviceDataDto> Devices { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
