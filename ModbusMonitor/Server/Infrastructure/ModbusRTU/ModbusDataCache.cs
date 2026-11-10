using System.Collections.Concurrent;

namespace Server.Infrastructure.ModbusRTU
{
    /// <summary>Modbus 数据缓存：双层并发字典，只存各测点最新值（设备Id → 测点Id → 值）</summary>
    public class ModbusDataCache
    {
        // 采集线程(多口并行写)与消费线程(报警/实时/落库/API读)并发访问，用线程安全容器
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, double>> _cache = new();

        /// <summary>取某设备的测点字典（返回缓存内部引用，实时值；IReadOnlyDictionary 封死写入口防篡改）</summary>
        public IReadOnlyDictionary<int, double>? GetDeviceData(int deviceId)
            => _cache.TryGetValue(deviceId, out var data) ? data : null;

        /// <summary>取全部数据快照（两层 ToDictionary 深拷贝新对象树，与 _cache 再无瓜葛；供 API 序列化，防传输期间被轮询改值）</summary>
        public Dictionary<int, Dictionary<int, double>> GetAllData()
            => _cache.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value)
            );

        public void UpdateDataPoint(int deviceId, int dataPointId, double value)
        {
            // GetOrAdd：设备字典不存在则原子创建（factory 并发时可能多造几个，但只有一份存活）
            // deviceCache 与 _cache 的 Value 是同一引用——直接写它就是写入缓存，无需写回
            var deviceCache = _cache.GetOrAdd(deviceId, _ => new ConcurrentDictionary<int, double>());
            // 索引器赋值：同键覆盖，天然"最新值胜出"
            deviceCache[dataPointId] = value;
        }

        /// <summary>清空缓存（value 是纯数据无句柄，Clear 断引用即可，旧数据由 GC 回收）</summary>
        public void Clear() => _cache.Clear();
    }
}
