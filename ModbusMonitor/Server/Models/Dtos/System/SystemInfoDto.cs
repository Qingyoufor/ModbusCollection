namespace Server.Models.Dtos
{
    public class SystemInfoDto
    {
        public string Version { get; set; } = "1.0.0";
        public DateTime ServerStartedAt { get; set; }
        public string UpTime { get; set; } = string.Empty;     // 已格式化的运行时长
        public int OnlineDeviceCount { get; set; }             // 缓存中有数据的设备数
        public int CachedPointCount { get; set; }              // 缓存测点总数
        public bool ModbusRunning { get; set; }
        public DateTime? LastPollTime { get; set; }
    }
}