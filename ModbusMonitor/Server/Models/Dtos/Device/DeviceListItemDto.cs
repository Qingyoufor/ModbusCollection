namespace Server.Models.Dtos
{
    // 轻量化的DeviceDto,保留核心属性
    public class DeviceListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
