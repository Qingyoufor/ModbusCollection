namespace Client.Models.Dtos
{
    using Client.Infrastructure;

    public class SystemInfoDto
    {
        public string Version { get; set; } = "1.0.0";
        public DateTime ServerStartedAt { get; set; }
        public string UpTime { get; set; } = string.Empty;
        public int OnlineDeviceCount { get; set; }
        public int CachedPointCount { get; set; }
        public bool ModbusRunning { get; set; }
        public DateTime? LastPollTime { get; set; }

        /// <summary>界面显示用的本地时间（服务端存的是 UTC）</summary>
        public DateTime ServerStartedAtLocal => ServerStartedAt.ToLocalFromUtc();

        /// <summary>界面显示用的本地时间（服务端存的是 UTC）</summary>
        public DateTime? LastPollTimeLocal => LastPollTime.ToLocalFromUtc();
    }
}