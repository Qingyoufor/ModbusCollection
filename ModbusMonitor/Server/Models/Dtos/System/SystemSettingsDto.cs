using System.ComponentModel.DataAnnotations;

namespace Server.Models.Dtos
{
    public class SystemSettingsDto
    {
        [Required]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}