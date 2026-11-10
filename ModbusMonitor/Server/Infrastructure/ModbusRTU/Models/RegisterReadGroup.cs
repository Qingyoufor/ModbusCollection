using Server.Models.Common;

namespace Server.Infrastructure.ModbusRTU.Models
{
    /// <summary>连续寄存器读取组：GroupingHelper 的产物，一个组对应一次读请求（组属性即帧字段）</summary>
    public class RegisterReadGroup
    {
        // 请求帧的"起始地址"字段（协议层偏移量，40001 已换算为 0 起）
        public int StartAddress { get; set; }
        // 请求帧的"寄存器数量"字段（数寄存器个数，非测点个数；Float/Int32 各占 2）
        public int GroupLength { get; set; }
        // 决定请求帧的功能码：HoldingRegister→03，InputRegister→04
        public RegisterType RegisterType { get; set; }
        // 不进帧：读回后按各测点偏移从响应数据里切片分发的本地记录
        public List<DataPointConfig> DataPoints { get; set; } = new();
    }
}
