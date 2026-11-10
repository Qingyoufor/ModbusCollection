using Server.Infrastructure.ModbusRTU.Models;
using Server.Models.Common;

namespace Server.Infrastructure.ModbusRTU
{
    public class ModbusGroupingHelper
    {
        /// <summary>按"寄存器类型一致 + 地址连续"将测点分组成连续段，把散点读取压缩成最少的整段请求（一次请求最多读125个寄存器且必须连续）</summary>
        public static List<RegisterReadGroup> GroupContinousRegister(List<DataPointConfig> points)
        {
            if (points.Count == 0)
                return new List<RegisterReadGroup>();

            // 排序后单遍扫描，断点即切组
            var sortedPoints = points.OrderBy(p => p.RegisterAddress).ToList();
            var groups = new List<RegisterReadGroup>();

            // 初始化第一组（游标 expectedNextAddr = 起始地址 + 该测点占用宽度）
            var groupStartAddr = sortedPoints[0].RegisterAddress;
            var groupPoints = new List<DataPointConfig> { sortedPoints[0] };
            var groupType = sortedPoints[0].RegisterType;
            int expectedNextAddr = groupStartAddr + GetRegisterLength(sortedPoints[0]);

            // 从第二个测点开始，断点才触发保存旧组、开新组
            for (int i = 1; i < sortedPoints.Count; i++)
            {
                var point = sortedPoints[i];
                int pointLength = GetRegisterLength(point);

                // 连续判据：类型一致（决定功能码03/04，帧内不可混）且地址等于游标（DataType不参与分组，只影响游标步进）
                if (point.RegisterType == groupType && point.RegisterAddress == expectedNextAddr)
                {
                    groupPoints.Add(point);
                    expectedNextAddr += pointLength;   // Float/Int32 占 2 个寄存器，游标步进 2
                }
                else
                {
                    // 断点：保存旧组（不含当前点）
                    groups.Add(new RegisterReadGroup
                    {
                        StartAddress = groupStartAddr,
                        GroupLength = expectedNextAddr - groupStartAddr,    // 长度=寄存器个数（非测点个数）
                        RegisterType = groupType,
                        DataPoints = groupPoints.ToList()   // 快照，防止后续复用变量时被连带修改
                    });

                    // 以当前点开新组
                    groupStartAddr = point.RegisterAddress;
                    groupPoints = new List<DataPointConfig> { point };
                    groupType = point.RegisterType;
                    expectedNextAddr = groupStartAddr + pointLength;
                }
            }

            // 最后一组没有断点触发保存，循环后补上
            groups.Add(new RegisterReadGroup
            {
                StartAddress = groupStartAddr,
                GroupLength = expectedNextAddr - groupStartAddr,
                RegisterType = groupType,
                DataPoints = groupPoints.ToList()
            });

            return groups;
        }

        /// <summary>单个测点占用的寄存器数：Int32/Float 占 2，其余占 1</summary>
        private static int GetRegisterLength(DataPointConfig point)
            => point.DataType == DataPointType.Int32 || point.DataType == DataPointType.Float ? 2 : 1;
    }
}
