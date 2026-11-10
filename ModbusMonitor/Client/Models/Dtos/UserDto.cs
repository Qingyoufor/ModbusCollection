using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
