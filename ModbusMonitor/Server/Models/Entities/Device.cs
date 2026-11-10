using Server.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models.Entities
{
    [Table("Devices")]
    public class Device
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }                    // 主键

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;// 设备名称

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty; // 设备编号，如 CNC-001

        [Range(1, 247)]
        public byte SlaveAddress { get; set; }         // 从站地址 (Modbus, 有效值1~247)

        public string ComPort { get; set; } = "COM2";  // 从站串口（COM）


        [MaxLength(200)]
        public string? Description { get; set; }       // 设备描述

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // 创建时间

        public ICollection<DataPoint> DataPoints { get; set; } = new List<DataPoint>(); // 设备下的测点
    }
}