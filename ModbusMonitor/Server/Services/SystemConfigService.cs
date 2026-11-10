using Server.Models.Dtos;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Server.Services
{
    /// <summary>setting.json 配置服务：读文件 → 扁平化进内存缓存；保存 = 改内存树 + 写回文件</summary>
    public class SystemConfigService : ISystemConfigService
    {
        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

        private readonly string _filePath;          // 主文件：随启动方式定位（dotnet run → 项目根；Server.exe → exe 所在目录）
        private readonly string? _syncPath;         // 同步文件：始终在可执行文件旁，保证两种启动方式下同一份配置
        private readonly ILogger<SystemConfigService> _logger;
        private readonly ConcurrentDictionary<string, string> _cache = new();
        private JsonObject? _root;   // setting.json 的原始嵌套结构（保存时整体写回）

        public SystemConfigService(IWebHostEnvironment env, ILogger<SystemConfigService> logger)
        {
            _filePath = Path.Combine(env.ContentRootPath, "setting.json");
            // AppContext.BaseDirectory 恒为 exe/dll 所在目录（与启动方式无关），与之不同时才需额外同步一份
            var basePath = Path.Combine(AppContext.BaseDirectory, "setting.json");
            _syncPath = string.Equals(Path.GetFullPath(basePath), Path.GetFullPath(_filePath), StringComparison.OrdinalIgnoreCase)
                ? null
                : basePath;
            _logger = logger;
        }

        public async Task LoadAsync()
        {
            if (!File.Exists(_filePath))
                throw new FileNotFoundException("缺少配置文件 setting.json", _filePath);

            var json = await File.ReadAllTextAsync(_filePath);
            _root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();

            // 扁平化：Modbus.BaudRate → "9600"，对象节点 → JSON 字符串
            _cache.Clear();
            Flatten(_root, prefix: string.Empty);

            _logger.LogInformation("系统配置加载完成（{Path}），共 {Count} 项", _filePath, _cache.Count);
        }

        public string? GetValueStr(string key)
            => _cache.TryGetValue(key, out var value) ? value : null;

        public int GetValueInt(string key, int defaultValue)
            => int.TryParse(GetValueStr(key), out var value) ? value : defaultValue;

        public async Task AddOrUpdateAsync(string key, string value)
        {
            _root ??= new JsonObject();
            SetValue(_root, key, value);

            await WriteAllAsync();
            _cache[key] = value;   // 先落盘再改缓存
        }

        public async Task AddOrUpdateAllAsync(List<SystemSettingsDto> settings)
        {
            _root ??= new JsonObject();
            foreach (var setting in settings)
                SetValue(_root, setting.Key, setting.Value);

            // 批量只写一次文件，避免循环写盘
            await WriteAllAsync();

            foreach (var setting in settings)
                _cache[setting.Key] = setting.Value;
        }

        // ===== 私有工具方法 =====

        /// <summary>整体写回主文件；启动方式不同（dotnet run / Server.exe）时同步一份到可执行文件旁，避免两处配置分叉</summary>
        private async Task WriteAllAsync()
        {
            var json = _root!.ToJsonString(_writeOptions);
            await File.WriteAllTextAsync(_filePath, json);

            if (_syncPath == null || !File.Exists(_syncPath))
                return;
            try
            {
                await File.WriteAllTextAsync(_syncPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("同步写入 {Path} 失败（不影响主文件）：{Message}", _syncPath, ex.Message);
            }
        }

        /// <summary>按点分路径写入节点："Modbus.BaudRate"=19200 → root["Modbus"]["BaudRate"]=19200。
        /// 整数字符串存数字节点；合法 JSON 对象/数组字符串（如 PortMappings）存为节点；其余存字符串</summary>
        private static void SetValue(JsonObject root, string key, string value)
        {
            var parts = key.Split('.');
            var current = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (current[parts[i]] is not JsonObject child)
                {
                    child = new JsonObject();
                    current[parts[i]] = child;
                }
                current = child;
            }

            JsonNode node = int.TryParse(value, out var n)
                ? JsonValue.Create(n)!
                : TryParseJson(value, out var parsed) ? parsed!
                : JsonValue.Create(value)!;
            current[parts[^1]] = node;
        }

        /// <summary>尝试把字符串解析为 JSON 对象/数组。返回 true 时 out 参数必不为 null（供编译器推导）</summary>
        private static bool TryParseJson(string value, [NotNullWhen(true)] out JsonNode? node)
        {
            try
            {
                node = JsonNode.Parse(value);
                return node is JsonObject or JsonArray;
            }
            catch (JsonException)
            {
                node = null;
                return false;
            }
        }

        /// <summary>递归扁平化：嵌套对象下钻，基元节点转字符串存入缓存</summary>
        private void Flatten(JsonObject obj, string prefix)
        {
            foreach (var (name, node) in obj)
            {
                var key = prefix.Length == 0 ? name : $"{prefix}.{name}";
                switch (node)
                {
                    // 容器对象（含嵌套对象/数组）→ 继续下钻，如 Modbus
                    case JsonObject inner when inner.Any(kv => kv.Value is JsonObject or JsonArray):
                        Flatten(inner, key);
                        break;
                    // 叶子对象（子节点全是基元，如 PortMappings / DataRetention）→ 整体存 JSON 字符串，
                    // 同时把基元子键再扁平化（DataRetention.DeviceDataDays 等），保证 GetSettings 能按点取键
                    case JsonObject leaf:
                        _cache[key] = leaf.ToJsonString();
                        Flatten(leaf, key);
                        break;
                    case JsonValue v when v.TryGetValue<string>(out var s):
                        _cache[key] = s;
                        break;
                    default:
                        _cache[key] = node?.ToString() ?? string.Empty;
                        break;
                }
            }
        }
    }
}