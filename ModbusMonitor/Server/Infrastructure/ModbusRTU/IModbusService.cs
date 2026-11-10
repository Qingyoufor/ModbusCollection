namespace Server.Infrastructure.ModbusRTU
{
    public interface IModbusService
    {
        /// <summary>获取所有设备的最新缓存数据（供 API 层查询）</summary>
        Dictionary<int, Dictionary<int, double>> GetCurrentData();

        /// <summary>手动触发重新加载设备配置</summary>
        Task ReloadConfigsAsync();

        /// <summary>Modbus 采集是否在运行</summary>
        bool IsRunning { get; }

        /// <summary>最近一次轮询完成时间（UTC）</summary>
        DateTime? LastPollTime { get; }
    }
}
       