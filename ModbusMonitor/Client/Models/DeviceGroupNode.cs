namespace Client.Models
{
    /// <summary>设备树的一级节点：按从站串口分组</summary>
    public class DeviceGroupNode
    {
        public string Name { get; set; } = string.Empty;
        public List<DeviceTreeNode> Devices { get; set; } = new();
    }
}
