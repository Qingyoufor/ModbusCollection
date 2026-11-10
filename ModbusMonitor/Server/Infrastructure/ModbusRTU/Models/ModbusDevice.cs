namespace Server.Infrastructure.ModbusRTU.Models
{
    public class ModbusDevice
    {
        public int DeviceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte SlaveAddress { get; set; }     // 从站地址 (1-247)
        public string SlaveCOM { get; set; } = string.Empty;  // 串口号
        public List<DataPointConfig> DataPoints { get; set; } = new();  // 该设备的测点配置
    }
}
