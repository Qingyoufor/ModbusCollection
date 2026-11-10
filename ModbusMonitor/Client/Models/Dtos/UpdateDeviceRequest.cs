using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class UpdateDeviceRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public byte SlaveAddress { get; set; } = 1;
        public string ComPort { get; set; } = "COM2";
        public string? Description { get; set; }
    }
}
