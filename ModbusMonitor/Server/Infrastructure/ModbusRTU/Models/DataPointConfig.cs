
using Server.Models.Common;

namespace Server.Infrastructure.ModbusRTU.Models
{
    public class DataPointConfig
    {
        public int DataPointId { get; set; }
        public string Name { get; set; } = string.Empty;

        public int RegisterAddress { get; set; }   // 寄存器地址（如 40001 → 实际偏移量）
        public RegisterType RegisterType { get; set; }  // 输入寄存器(3)/保持寄存器(4)
        public DataPointType DataType { get; set; }     // Int16/UInt16/Int32/Float

        public double ScaleFactor { get; set; } = 1.0;  // 缩放因子
        public int DecimalPlaces { get; set; } = 0;     // 小数位数
        public double MinValue { get; set; }             // 最小值（范围校验）
        public double MaxValue { get; set; }             // 最大值（范围校验）
    }
}
