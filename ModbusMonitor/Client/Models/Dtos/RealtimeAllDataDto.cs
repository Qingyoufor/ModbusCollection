using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class RealtimeAllDataDto
    {
        public List<RealtimeDeviceDataDto> Devices { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
