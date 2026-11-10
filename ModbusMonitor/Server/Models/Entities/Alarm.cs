using Server.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models.Entities;

[Table("Alarms")]
public class Alarm
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }             // 主键

    public int DeviceId { get; set; }        // 设备外键
    [ForeignKey(nameof(DeviceId))]
    public Device? Device { get; set; }      // 所属设备

    public int DataPointId { get; set; }     // 测点外键
    [ForeignKey(nameof(DataPointId))]
    public DataPoint? DataPoint { get; set; }// 所属测点


    public AlarmType AlarmType { get; set; }                   // 报警类型: 0高限/1低限

    [Required]
    [MaxLength(200)]
    public string Message { get; set; } = string.Empty; // 报警描述

    public double Value { get; set; }        // 触发时的实时值
    public double Threshold { get; set; }    // 触发报警值快照

    public AlarmStatus Status { get; set; } = AlarmStatus.Unacknowledged;  // 状态: 0未确认/1已确认

    [MaxLength(50)]
    public string? AcknowledgedBy { get; set; }   // 确认人

    public DateTime? AcknowledgedAt { get; set; }// 确认时间
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;  // 发生时间
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;   // 创建时间
}