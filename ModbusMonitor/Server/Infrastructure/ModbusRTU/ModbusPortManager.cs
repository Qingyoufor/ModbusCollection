using NModbus;
using NModbus.Serial;
using Server.Services;
using System.Collections.Concurrent;
using System.IO.Ports;

namespace Server.Infrastructure.ModbusRTU
{
    /// <summary>串口管理器：串口池 + Master 池（一个串口对应一个 Master，统一生命周期管理）</summary>
    public class ModbusPortManager : IDisposable
    {
        private readonly ILogger<ModbusPortManager> _logger;
        private readonly ISystemConfigService _configService;
        // 轮询线程(读)与API触发的配置重载线程(写)存在并发访问，必须用线程安全容器
        private readonly ConcurrentDictionary<string, SerialPort> _portPool = new(); // <COM口，对应的SerialPort>
        private readonly ConcurrentDictionary<string, IModbusSerialMaster> _masterCache = new(); // <COM口, Master实例>
        private Dictionary<string, string> _slaveToMaster = new(); // 从站→主站映射缓存（InitializePorts 时刷新）

        public ModbusPortManager(ILogger<ModbusPortManager> logger, ISystemConfigService configService)
        {
            _logger = logger;
            _configService = configService;
        }

        /// <summary>初始化串口连接池,如{<COM1,Port1>,<COM3,Port2>} </summary>
        public void InitializePorts(IEnumerable<string> slaveCOMs)
        {
            _slaveToMaster = BuildComMapping(); // 刷新映射缓存

            foreach (var slaveCOM in slaveCOMs.Distinct())
            {
                // 1. 根据从站端口找主站端口
                if (!_slaveToMaster.TryGetValue(slaveCOM, out var masterCOM))
                {
                    _logger.LogWarning("从站端口 {SlavePort} 无对应的主站端口映射", slaveCOM);
                    continue;
                }

                // 2. 检查是否已打开(池里有且口还开着才跳过)
                if (_portPool.TryGetValue(masterCOM, out var existing) && existing.IsOpen)
                {
                    _logger.LogDebug("主站端口 {MasterPort} 已打开，跳过", masterCOM);
                    continue;
                }

                // 池里有但已意外关闭(如USB串口被拔)：释放残留实例，走下方正常打开流程实现自愈
                if (existing != null)
                {
                    _portPool.TryRemove(masterCOM, out _);
                    try { existing.Dispose(); } catch { }
                }

                try
                {
                    // 3. 创建并打开串口
                    var port = CreateSerialPort(masterCOM);
                    port.Open();
                    _portPool[masterCOM] = port; // 存入主站缓存池
                    _logger.LogInformation("主站端口 {MasterPort} 打开成功（对应从站 {SlavePort}）", masterCOM, slaveCOM);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "主站端口 {MasterPort} 打开失败", masterCOM);
                }
            }
        }

        /// <summary>构建端口映射：从站端口 → 主站端口（来自 setting.json）</summary>
        public Dictionary<string, string> BuildComMapping()
        {
            // PortMappings 在 setting.json 里是嵌套对象，缓存中以其 JSON 文本形式存储
            var json = _configService.GetValueStr("Modbus.PortMappings");

            // 键缺失 → 空映射；反序列化结果为 null（如 JSON 字面量 "null"）也兜底空映射
            return string.IsNullOrEmpty(json)
                ? new Dictionary<string, string>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                  ?? new Dictionary<string, string>();
        }

        /// <summary>获取已打开的串口</summary>
        public SerialPort? GetPort(string masterCOM)
            => _portPool.TryGetValue(masterCOM, out var port) && port.IsOpen ? port : null;

        /// <summary>根据从站端口查对应的主站端口（走缓存，无映射时返回 null）</summary>
        public string? GetMasterComBySlaveCom(string slaveCOM)
            => _slaveToMaster.TryGetValue(slaveCOM, out var master) ? master : null;

        /// <summary>创建串口实例，SerialPort封装了Windows API操作端口</summary>
        private SerialPort CreateSerialPort(string portName)
        {
            var (baudRate, dataBits, stopBits, parity, readTimeout, writeTimeout) = GetPortSettings();
            return new SerialPort(portName)
            {
                BaudRate = baudRate,
                DataBits = dataBits,
                StopBits = stopBits,
                Parity = parity,
                ReadTimeout = readTimeout,
                WriteTimeout = writeTimeout
            };
        }

        /// <summary>读取串口配置（全部来自 setting.json，键缺失时用常量兜底）</summary>
        private (int baudRate, int dataBits, StopBits stopBits, Parity parity, int readTimeout, int writeTimeout)
            GetPortSettings()
            => (
                _configService.GetValueInt("Modbus.BaudRate", 9600),
                _configService.GetValueInt("Modbus.DataBits", 8),
                Enum.Parse<StopBits>(_configService.GetValueStr("Modbus.StopBits") ?? "One"),
                Enum.Parse<Parity>(_configService.GetValueStr("Modbus.Parity") ?? "None"),
                _configService.GetValueInt("Modbus.ReadTimeout", 500),
                _configService.GetValueInt("Modbus.WriteTimeout", 500)
            );


        /// <summary>获取或创建指定串口的 Modbus Master（NModbus 3.0.83：Adapter→Transport→Master 三层包装）</summary>
        public IModbusSerialMaster GetOrCreateMaster(SerialPort port)
        {
            if (_masterCache.TryGetValue(port.PortName, out var master))
                return master;

            var adapter = new SerialPortAdapter(port);           // 把 SerialPort 包装成 IStreamResource
            var factory = new ModbusFactory();                   // 工厂类
            var transport = factory.CreateRtuTransport(adapter); // 创建 RTU 传输层（处理帧格式和 CRC）
            master = factory.CreateMaster(transport);            // 创建 Master（业务层）

            _masterCache[port.PortName] = master;
            _logger.LogDebug("创建 Modbus Master: {Port}", port.PortName);
            return master;
        }

        /// <summary>统一释放：先清 Master（引用着串口），再关串口</summary>
        public void Dispose()
        {
            foreach (var master in _masterCache.Values)
            {
                try { master.Dispose(); } catch { }
            }
            _masterCache.Clear();

            foreach (var port in _portPool.Values)
            {
                try
                {
                    if (port.IsOpen)
                        port.Close();
                    port.Dispose();
                }
                catch { }
            }
            _portPool.Clear();
        }
    }
}