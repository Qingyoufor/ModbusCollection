using Client.Service;
using System.Net.Http;
using System.Windows.Controls;

namespace Client.ViewModels
{
    public class LoginViewModel	: BindableBase
    {
		private readonly IAuthService _authService;
        private readonly IShellService _shellService;

		private string _username = string.Empty;
        private string _password = string.Empty;
        private bool _hasError;
        private string? _errorMessage;

        public AsyncDelegateCommand<object> LoginCommand { get; }

        public LoginViewModel(IAuthService authService, IShellService shellService)
        {
			_authService = authService;
            _shellService = shellService;
			LoginCommand = new AsyncDelegateCommand<object>(ExecuteLogin);
        }

        public string Username
		{
			get => _username;
			set	=> SetProperty(ref _username, value);
		}
		public string Password
		{
			get => _password;
			set => SetProperty(ref _password, value);
		}
		public bool HasError
        {
			get => _hasError;
			set => SetProperty(ref _hasError, value);
		}
		public string? ErrorMessage
        {
			get => _errorMessage;
			set => SetProperty(ref _errorMessage, value);
		}
		
		// 登录方法
		private async Task ExecuteLogin(object parameter)
		{
			if(!(parameter is PasswordBox passwordBox)) return;

			Password = passwordBox.Password;

			if(string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Username))
			{
				await ShowErrorMessageAsync("账号或密码不能为空");
				return;
			}

			try
			{
                var success = await _authService.SignInAsync(Username, Password);
                if (success)
                {
                    // 登陆成功跳转到ShellView
                    _shellService.LoginToShell();
                }
                else
                {
                    await ShowErrorMessageAsync("账号或密码错误");
                }
            }
            catch (HttpRequestException)
            {
                await ShowErrorMessageAsync("无法连接服务器，请检查网络");
            }
            catch (Exception)
            {
                await ShowErrorMessageAsync("登录失败，请稍后重试");
            }
        }

		// 显示错误信息
		private async Task ShowErrorMessageAsync(string message)
		{
            HasError = true;
            ErrorMessage = message;
            await Task.Delay(2000);
            HasError = false;
            ErrorMessage = string.Empty;
        }

        
    }
}
