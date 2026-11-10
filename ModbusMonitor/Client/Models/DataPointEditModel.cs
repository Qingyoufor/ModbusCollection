using Client.Models.Dtos;

namespace Client.Models
{
    /// <summary>设备编辑界面里的一行测点：已有测点只读，新加的行可编辑</summary>
    public class DataPointEditModel : BindableBase
    {
        private string _name = string.Empty;
        private int _registerAddress;
        private RegisterType _registerType = RegisterType.HoldingRegister;
        private DataPointType _dataType = DataPointType.Int16;
        private string? _unit;

        public int Id { get; init; }

        /// <summary>服务端保存的排序号，新增测点接在已有最大值之后</summary>
        public int SortOrder { get; init; }

        /// <summary>本次新增的行（保存时提交创建）</summary>
        public bool IsNew { get; init; }

        /// <summary>已存在于数据库的行（界面上只读）</summary>
        public bool IsExisting => !IsNew;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int RegisterAddress
        {
            get => _registerAddress;
            set => SetProperty(ref _registerAddress, value);
        }

        public RegisterType RegisterType
        {
            get => _registerType;
            set => SetProperty(ref _registerType, value);
        }

        public DataPointType DataType
        {
            get => _dataType;
            set => SetProperty(ref _dataType, value);
        }

        public string? Unit
        {
            get => _unit;
            set => SetProperty(ref _unit, value);
        }

        public static DataPointEditModel FromDto(DataPointDto dto) => new()
        {
            Id = dto.Id,
            SortOrder = dto.SortOrder,
            IsNew = false,
            Name = dto.Name,
            RegisterAddress = dto.RegisterAddress,
            RegisterType = Enum.TryParse<RegisterType>(dto.RegisterType, out var registerType) ? registerType : RegisterType.HoldingRegister,
            DataType = Enum.TryParse<DataPointType>(dto.DataType, out var dataType) ? dataType : DataPointType.Int16,
            Unit = dto.Unit
        };

        public CreateDataPointRequest ToCreateRequest(int sortOrder) => new()
        {
            Name = Name.Trim(),
            RegisterAddress = RegisterAddress,
            RegisterType = RegisterType,
            DataType = DataType,
            Unit = string.IsNullOrWhiteSpace(Unit) ? null : Unit.Trim(),
            MinValue = 0,
            MaxValue = 100,
            ScaleFactor = 1.0,
            DecimalPlaces = 0,
            SortOrder = sortOrder
        };
    }
}
