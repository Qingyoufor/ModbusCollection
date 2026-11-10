using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class CreateDeviceRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public byte SlaveAddress { get; set; }
        public string ComPort { get; set; } = "COM2";
        public string? Description { get; set; }
    }
}
