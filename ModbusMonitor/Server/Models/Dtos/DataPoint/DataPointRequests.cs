using Server.Models.Common;
using Server.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace Server.Models.Dtos
{
    public record CreateDataPointRequest
    {
        [Required] public string Name { get; init; } = string.Empty;
        public int RegisterAddress { get; init; }
        public RegisterType RegisterType { get; init; } = RegisterType.HoldingRegister;
        public DataPointType DataType { get; init; } = DataPointType.Int16;
        public double MinValue { get; init; }
        public double MaxValue { get; init; }
        public string? Unit { get; init; }
        public double? AlarmHigh { get; init; }
        public double? AlarmLow { get; init; }
        public double ScaleFactor { get; init; } = 1.0;
        public int DecimalPlaces { get; init; }
        public int SortOrder { get; init; }
    }

    public record UpdateDataPointRequest
    {
        [Required] public string Name { get; init; } = string.Empty;
        public int RegisterAddress { get; init; }
        public RegisterType RegisterType { get; init; } = RegisterType.HoldingRegister;
        public DataPointType DataType { get; init; } = DataPointType.Int16;
        public double MinValue { get; init; }
        public double MaxValue { get; init; }
        public string? Unit { get; init; }
        public double? AlarmHigh { get; init; }
        public double? AlarmLow { get; init; }
        public double ScaleFactor { get; init; } = 1.0;
        public int DecimalPlaces { get; init; }
        public int SortOrder { get; init; }
    }
}
