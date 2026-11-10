using System.ComponentModel.DataAnnotations;

namespace Server.Models.Dtos
{
    public class RefreshTokenRequest
    {
        [Required]
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
