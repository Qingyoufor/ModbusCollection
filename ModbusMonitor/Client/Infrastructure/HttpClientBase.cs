using Client.Models.Dtos;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Client.Infrastructure
{
    /*
     HttpClient 负责：联系前后端
       - 建立 TCP 连接
       - 发送 HTTP 请求报文
       - 接收 HTTP 响应报文
       - 解析成 HttpResponseMessage
     */
    public class HttpClientBase
    {
        private readonly HttpClient _httpClient;
        private readonly TokenStore _tokenStore;
        private readonly SemaphoreSlim _refreshTokenLock = new(1, 1); //异步锁，只允许一个线程进入信号量锁
        private static readonly JsonSerializerOptions _jsonOptions = new () { PropertyNameCaseInsensitive = true };

        private const string BaseUrl = "http://localhost:5078/";

        public HttpClientBase(TokenStore tokenStore)
        {
            // 和相对路径进行拼接，如"api/auth/refresh"
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
            _tokenStore = tokenStore;
        }

        //===========公开的 HTTP CRUD 方法==========

        public async Task<HttpResponseMessage> GetAsync(string url)
        {
            return await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url)); 
        }

        /// <summary>POST 请求，每次重建 request，避免 401 刷新重放时复用同一实例（一次性流 body 会崩）</summary>
        public async Task<HttpResponseMessage> PostAsync<T>(string url,T body)
        {
            return await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body, options: _jsonOptions)
            }); 
        }

        /// <summary>PUT 请求，同 POST，重放时需重建新的 request 实例</summary>
        public async Task<HttpResponseMessage> PutAsync<T>(string url, T body)
        {
            return await SendAsync(() => new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(body, options: _jsonOptions)
            }); 
        }

        /// <summary>DELETE 请求，无 body，重放安全</summary>
        public async Task<HttpResponseMessage> DeleteAsync(string url)
        {
            return await SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, url));
        }




        // ======核心发送逻辑=======

        /// <summary>统一请求入口：自动附加 token，遇 401 刷新后重放一次</summary>
        private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory) 
        {
            // 1.首次发送
            var request = requestFactory();
            AttachToken(request);
            var respone = await _httpClient.SendAsync(request);

            // 2.如果401，尝试刷新Token
            if(respone.StatusCode == HttpStatusCode.Unauthorized)
            {
                var success = await TryRefreshTokenAsync();
                // 如果刷新成功，则重放原请求
                if (success)
                {
                    request = requestFactory();
                    AttachToken(request);
                    respone = await _httpClient.SendAsync(request);
                }
            }

            return respone;
        }

        /// <summary>给请求附加 Bearer token（token 非空时才带上）</summary>
        private void AttachToken(HttpRequestMessage request) 
        {
            var accessToken = _tokenStore.AccessToken;
            if(!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        /// <summary>通过 refresh token 换取新 token，成功返回 true（多线程下异步锁互斥，只刷新一次）</summary>
        private async Task<bool> TryRefreshTokenAsync() 
        {
            await _refreshTokenLock.WaitAsync(); // 获取钥匙(锁)
            try
            {
                // 请求刷新token（可以在此方法前添加token过期识别，如果过期了再进行下面的刷新操作）
                var response = await _httpClient.PostAsJsonAsync("api/auth/refresh", new TokenStore
                {
                    AccessToken = _tokenStore.AccessToken,
                    RefreshToken = _tokenStore.RefreshToken
                }, _jsonOptions); 
                
                if(!response.IsSuccessStatusCode) return false;

                // json反序列化成目标泛行
                var result = await response.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>(_jsonOptions);
                if(result == null || result.Data == null || result.Code != 200) return false;

                _tokenStore.SetTokens(result.Data.AccseeToken, result.Data.RefreshToken);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _refreshTokenLock.Release(); // 释放钥匙
            }
        }

       
    }
}
