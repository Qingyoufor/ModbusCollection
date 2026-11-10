using Server.Infrastructure.ModbusRTU.Models;
using Server.Models.Common;

namespace Server.Infrastructure.ModbusRTU
{
    public class ModbusDataParser
    {
        /// <summary>
        /// 从 Modbus 寄存器原始值解析为实际工程值
        /// 流程：原始寄存器值 × ScaleFactor → 范围校验 → 四舍五入到指定小数位
        /// </summary>
        public static double ParseRawValue(ushort[] registers, DataPointConfig point)
        {
            // register 数组长度：Int16/UInt16 => 1 Int32/Float => 2
            double rawValue = point.DataType switch
            {
                DataPointType.Int16 => (short)registers[0],
                DataPointType.UInt16 => registers[0],
                DataPointType.Int32 => (int)((uint)registers[0] << 16 | registers[1]),
                DataPointType.Float => ParseFloatBigEndian(registers), // 浮点数考虑大小端序
                _ => registers[0]
            };

            double scaleValue = rawValue * point.ScaleFactor;
            return Math.Round(scaleValue, point.DecimalPlaces);
        }

        /// <summary>
        /// 按大端序解析 Float：高字在前（Modbus RTU 规定大端）
        /// 用 Int32BitsToSingle 对拼好的 32 位做"位重解释"，不经过字节数组，平台无关
        /// </summary>
        private static float ParseFloatBigEndian(ushort[] registers)
            => BitConverter.Int32BitsToSingle((int)((uint)registers[0] << 16 | registers[1]));

        /// <summary>校验解析后的值是否在合法范围内</summary>
        public static bool IsInRange(double value, DataPointConfig point)
            => value >= point.MinValue && value <= point.MaxValue;
    }
}