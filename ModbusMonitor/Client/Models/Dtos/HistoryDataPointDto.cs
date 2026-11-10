using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class HistoryDataPointDto
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public int DataPointId { get; set; }
        public string DataPointName { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; }
        public double Value { get; set; }
    }
}
