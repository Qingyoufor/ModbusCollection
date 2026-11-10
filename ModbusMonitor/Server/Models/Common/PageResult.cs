namespace Server.Models.Common
{
    //分页结果
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }   //数据总条数
        public int Page { get; set; }    //当前页码
        public int PageSize { get; set; }   //页数据条数
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize); //向上取整，总页数
        public bool HasPrevious => Page > 1;   //是否有上一页
        public bool HasNext => Page < TotalPages;    //是否有下一页
    }
}
