using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class SignInRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
