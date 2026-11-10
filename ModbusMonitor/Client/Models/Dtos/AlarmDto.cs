using Client.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class AlarmDto
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public int DataPointId { get; set; }
        public string? DataPointName { get; set; }

        public string AlarmType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Threshold { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }

        /// <summary>界面显示用的本地时间（服务端存的是 UTC）</summary>
        public DateTime OccurredAtLocal => OccurredAt.ToLocalFromUtc();
    }
}
