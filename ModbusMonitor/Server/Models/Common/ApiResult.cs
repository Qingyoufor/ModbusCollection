namespace Server.Models.Common
{
    public class ApiResult<T>
    {
        //是否请求成功
        public int Code { get; set; } = 200;
        public string Message { get; set; } = "Success";
        //T对象数据
        public T? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        //静态Success/Fail方法
        public static ApiResult<T> Success(T data, string message = "Success")
            => new() { Code = 200, Message = message, Data = data };

        public static ApiResult<T> Fail(int code, string message)
            => new() { Code = code, Message = message };
    }

    //继承ApiResult<object>获取Code、Message等成员
    public class ApiResult : ApiResult<object>
    {
        public static ApiResult Success(string message = "Success")
            => new() { Code = 200, Message = message };

        //方法名和方法参数和父类方法重合时，需要new来隐藏父类方法
        public static new ApiResult Fail(int code, string message)
            => new() { Code = code, Message = message };
    }
}
