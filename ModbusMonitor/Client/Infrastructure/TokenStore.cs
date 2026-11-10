using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Infrastructure
{
    // lock-关键字 _lock相当于钥匙
    // lock (_lock) 拿钥匙
    // {
    //    这个代码块被钥匙锁住了
    //    只有拿到钥匙的线程能进来
    // }还钥匙（自动）
    public class TokenStore
    {
        private string? _accessToken;
        private string? _refreshToken;
        private readonly object _lock = new(); // 同步锁，共用一把锁

        public string? AccessToken
        {
            get { lock (_lock) { return _accessToken; } }
            set { lock (_lock) { _accessToken = value; } }
        }
        public string? RefreshToken
        {
            get { lock (_lock) return _refreshToken; }
            set { lock (_lock) _refreshToken = value; }
        }

        public void SetTokens(string accessToken, string refreshToken)
        {
            lock (_lock)
            {
                _accessToken = accessToken;
                _refreshToken = refreshToken;
            }
        }
        public void ClearTokens()
        {
            lock(_lock)
            {
                _accessToken = null; 
                _refreshToken = null;
            }
        }
    }
}
