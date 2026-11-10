
using System.Windows.Media;

namespace Client.Models
{
    public class DataPointDisplayModel
    {
        public int DataPointId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string? Unit { get; set; }
        public int DecimalPlaces { get; set; }
        public string AlarmStatus { get; set; } = "Normal";
        public double? AlarmHigh { get; set; }
        public double? AlarmLow { get; set; }

        /// <summary>格式化显示值（按 DecimalPlaces 截断）</summary>
        public string DisplayValue => Value.ToString($"F{DecimalPlaces}");

        /// <summary>数值颜色：High=红 / Low=橙 / Normal=黑</summary>
        public Color ValueColor => AlarmStatus switch
        {
            "High" => Color.FromRgb(0xF4, 0x43, 0x36), // #F44336
            "Low" => Color.FromRgb(0xFF, 0x98, 0x00),  // #FF9800
            _ => Color.FromRgb(0x33, 0x33, 0x33)        // #333333
        };

        /// <summary>报警提示文本</summary>
        public string AlarmText => AlarmStatus switch
        {
            "High" => $"⚠ 高限报警 (>{AlarmHigh})",
            "Low" => $"⚠ 低限报警 (<{AlarmLow})",
            _ => "正常"
        };

        /// <summary>报警提示颜色</summary>
        public Color AlarmColor => AlarmStatus switch
        {
            "High" => Color.FromRgb(0xF4, 0x43, 0x36),
            "Low" => Color.FromRgb(0xFF, 0x98, 0x00),
            _ => Color.FromRgb(0x4C, 0xAF, 0x50)  // 绿色
        };
    }
}