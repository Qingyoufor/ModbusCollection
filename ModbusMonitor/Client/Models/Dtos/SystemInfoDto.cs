namespace Client.Models.Dtos
{
    public class SystemInfoDto
    {
        public string Version { get; set; } = "1.0.0";
        public DateTime ServerStartedAt { get; set; }
        public string UpTime { get; set; } = string.Empty;
        public int OnlineDeviceCount { get; set; }
        public int CachedPointCount { get; set; }
        public bool ModbusRunning { get; set; }
        public DateTime? LastPollTime { get; set; }
    }
}