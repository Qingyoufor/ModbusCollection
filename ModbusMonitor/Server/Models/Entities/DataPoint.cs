using Server.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models.Entities
{
    /// <summary>设备测点（采集的最小单元）</summary>
    [Table("DataPoints")]
    public class DataPoint
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }                    // 主键

        public int DeviceId { get; set; }              // 设备外键
        [ForeignKey(nameof(DeviceId))]
        public Device? Device { get; set; }            // 所属设备

        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;  // 测点名称

        public int RegisterAddress { get; set; }       // 寄存器地址（PLC绝对地址）

        public RegisterType RegisterType { get; set; } = RegisterType.HoldingRegister;  // 寄存器类型
        public DataPointType DataType { get; set; } = DataPointType.Int16;              // 数据类型

        public double MinValue { get; set; }           // 量程下限
        public double MaxValue { get; set; }           // 量程上限

        [MaxLength(20)]
        public string? Unit { get; set; }              // 单位

        public double? AlarmHigh { get; set; }         // 高限报警阈值（null=不启用）
        public double? AlarmLow { get; set; }          // 低限报警阈值（null=不启用）

        public double ScaleFactor { get; set; } = 1.0; // 缩放系数
        public int DecimalPlaces { get; set; } = 0;    // 小数位

        public int SortOrder { get; set; }             // 排序号（按此排序显示）
    }
}