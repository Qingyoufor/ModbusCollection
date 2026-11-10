using Server.Models.Dtos;

namespace Server.Services
{
    public interface ISystemConfigService
    {
        /// <summary>启动时调用一次：把 setting.json 全量配置加载进内存缓存</summary>
        Task LoadAsync();

        /// <summary>从内存缓存读配置（线程安全），不存在返回 null</summary>
        string? GetValueStr(string key);

        /// <summary>读取 int 配置，解析失败返回默认值</summary>
        int GetValueInt(string key, int defaultValue);

        /// <summary>保存（新增或更新）单个配置：改内存树 + 写回文件 + 同步缓存</summary>
        Task AddOrUpdateAsync(string key, string value);

        /// <summary>批量保存配置（只写一次文件）</summary>
        Task AddOrUpdateAllAsync(List<SystemSettingsDto> settings);
    }
}