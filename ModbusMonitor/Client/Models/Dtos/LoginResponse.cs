using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class LoginResponse
    {
        public string AccseeToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto? User { get; set; }
    }
}
