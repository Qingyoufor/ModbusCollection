namespace Client.Infrastructure
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// 服务端统一存 UTC，但 SQL Server 读回的 DateTime 是 Unspecified，
        /// 直接调用 ToLocalTime() 不会做任何转换。这里先补上 UTC 标记再转本地。
        /// </summary>
        public static DateTime ToLocalFromUtc(this DateTime value) =>
            value.Kind == DateTimeKind.Local
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();

        /// <summary>可空版本：值为空时保持为空</summary>
        public static DateTime? ToLocalFromUtc(this DateTime? value) =>
            value?.ToLocalFromUtc();
    }
}
