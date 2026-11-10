using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Client.Models
{
    /// <summary>测点显示模型：属性可变更并通知界面，供每秒刷新时就地更新（不重建对象）</summary>
    public class DataPointDisplayModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private double _value;
        private string? _unit;
        private int _decimalPlaces;
        private string _alarmStatus = "Normal";
        private double? _alarmHigh;
        private double? _alarmLow;

        public int DataPointId { get; set; }

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public double Value
        {
            get => _value;
            set
            {
                if (SetField(ref _value, value))
                    RaisePropertyChanged(nameof(DisplayValue));
            }
        }

        public string? Unit
        {
            get => _unit;
            set => SetField(ref _unit, value);
        }

        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set
            {
                if (SetField(ref _decimalPlaces, value))
                    RaisePropertyChanged(nameof(DisplayValue));
            }
        }

        public string AlarmStatus
        {
            get => _alarmStatus;
            set
            {
                if (SetField(ref _alarmStatus, value))
                {
                    RaisePropertyChanged(nameof(ValueColor));
                    RaisePropertyChanged(nameof(AlarmColor));
                    RaisePropertyChanged(nameof(AlarmText));
                    RaisePropertyChanged(nameof(StatusText));
                    RaisePropertyChanged(nameof(IsAlarm));
                }
            }
        }

        public double? AlarmHigh
        {
            get => _alarmHigh;
            set
            {
                if (SetField(ref _alarmHigh, value))
                    RaisePropertyChanged(nameof(AlarmText));
            }
        }

        public double? AlarmLow
        {
            get => _alarmLow;
            set
            {
                if (SetField(ref _alarmLow, value))
                    RaisePropertyChanged(nameof(AlarmText));
            }
        }

        /// <summary>是否处于报警状态（高限或低限）</summary>
        public bool IsAlarm => AlarmStatus is "High" or "Low";

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

        /// <summary>表格用的简短状态文本</summary>
        public string StatusText => AlarmStatus switch
        {
            "High" => "高限报警",
            "Low" => "低限报警",
            _ => "正常"
        };

        /// <summary>报警提示颜色</summary>
        public Color AlarmColor => AlarmStatus switch
        {
            "High" => Color.FromRgb(0xF4, 0x43, 0x36),
            "Low" => Color.FromRgb(0xFF, 0x98, 0x00),
            _ => Color.FromRgb(0x4C, 0xAF, 0x50)  // 绿色
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void RaisePropertyChanged(string? propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
