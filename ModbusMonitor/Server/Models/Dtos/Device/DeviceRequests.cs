using System.ComponentModel.DataAnnotations;

namespace Server.Models.Dtos
{
    public record CreateDeviceRequest
    {
        [Required]
        public string Name { get; init; } = string.Empty;
        [Required]
        public string Code { get; init; } = string.Empty;
        public byte SlaveAddress { get; init; } = 1;
        public string ComPort { get; init; } = "COM3";
        public string? Description { get; init; }
    }

    public record UpdateDeviceRequest
    {
        [Required]
        public string Name { get; init; } = string.Empty;
        [Required]
        public string Code { get; init; } = string.Empty;
        public byte SlaveAddress { get; init; } = 1;
        public string ComPort { get; init; } = "COM3";
        public string? Description { get; init; }
    }
}
