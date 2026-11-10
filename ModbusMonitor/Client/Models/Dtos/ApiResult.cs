using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models.Dtos
{
    public class ApiResult<T>
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class ApiResult : ApiResult<object> { }
}
