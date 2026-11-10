using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models.Entities;

/// <summary>设备历史数据记录</summary>
[Table("DeviceDatas")]
public class DeviceData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }                    // 主键

    public int DeviceId { get; set; }              // 设备外键
    [ForeignKey(nameof(DeviceId))]
    public Device? Device { get; set; }            // 所属设备

    public int DataPointId { get; set; }           // 测点外键
    [ForeignKey(nameof(DataPointId))]
    public DataPoint? DataPoint { get; set; }      // 所属测点

    public double Value { get; set; }              // 数据值

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;  // 采集时间
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;   // 入库时间
}