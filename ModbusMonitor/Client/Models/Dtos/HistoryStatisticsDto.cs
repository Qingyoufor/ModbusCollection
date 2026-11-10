using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class HistoryStatisticsDto
    {
        public double Max { get; set; }
        public double Min { get; set; }
        public double Average { get; set; }
        public int Count { get; set; }
    }
}
