using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Service
{
    public interface IAuthService
    {
        Task<bool> SignInAsync(string username,string password);
        Task LogoutAsync();
    }
}
