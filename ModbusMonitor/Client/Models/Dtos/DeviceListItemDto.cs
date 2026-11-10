using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class DeviceListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
