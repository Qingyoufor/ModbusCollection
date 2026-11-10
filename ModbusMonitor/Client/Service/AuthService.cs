using Client.Infrastructure;
using Client.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Client.Service
{
    public class AuthService : IAuthService
    {
        private readonly HttpClientBase _http;
        private readonly TokenStore _tokenStore;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public AuthService(HttpClientBase httpClient,TokenStore tokenStore)
        {
            _http = httpClient;
            _tokenStore = tokenStore;
        }

        // 登录
        public async Task<bool> SignInAsync(string username, string password)
        {
            var response = await _http.PostAsync("api/auth/signIn", new SignInRequest
            {
                Username = username,
                Password = password
            });

            if(!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>(_jsonOptions);

            if (result == null || result.Data == null || result.Code != 200) return false;
            _tokenStore.SetTokens(result.Data.AccseeToken, result.Data.RefreshToken);

            return true;
        }

        // 登出
        public async Task LogoutAsync()
        {
            try
            {
                await _http.PostAsync<object>("api/auth/logout", new { });
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                // 即使服务端注销失败，本地 Token 也清空
                _tokenStore.ClearTokens();
            }
        }
    }
}
