using Server.Infrastructure.ModbusRTU.Models;
using Server.Services;

namespace Server.Infrastructure.ModbusRTU
{
    /// <summary>Modbus 采集主服务：Host 启动即自转的轮询引擎，编排各协作组件（对 Host 是 HostedService，对业务是 IModbusService）</summary>
    public class ModbusService : BackgroundService, IModbusService
    {
        private readonly ILogger<ModbusService> _logger;
        private readonly ISystemConfigService _systemConfigService;   // setting.json 配置缓存（串口参数/轮询间隔/端口映射）
        private readonly ModbusConfigLoader _configLoader;          // 从数据库加载设备采集配置
        private readonly ModbusPortManager _portManager;            // 串口池 + Master 池（通信资源）
        private readonly ModbusDataReader _dataReader;              // 实际读取/解析/写缓存
        private readonly ModbusDataCache _cache;                    // 最新值缓存（消费方：实时/落库/报警）

        private List<ModbusDevice>? _modbusDevices;   // 从数据库加载的采集设备清单（地址/串口/测点）

        public bool IsRunning { get; private set; }         // 采集循环是否在跑（健康检查用）
        public DateTime? LastPollTime { get; private set; } // 最后一轮完成时间

        public ModbusService(
            ILogger<ModbusService> logger,
            ISystemConfigService systemConfigService,
            ModbusConfigLoader configLoader,
            ModbusPortManager portManager,
            ModbusDataReader dataReader,
            ModbusDataCache cache)
        {
            _logger = logger;
            _systemConfigService = systemConfigService;
            _configLoader = configLoader;
            _portManager = portManager;
            _dataReader = dataReader;
            _cache = cache;
        }

        /// <summary>主循环：加载配置 → 打开串口 → 循环"轮询全部设备 + 等间隔"。单轮失败只记日志不杀循环</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Modbus 采集服务启动中...");

            // 1.加载设备配置
            await _systemConfigService.LoadAsync();

            _modbusDevices = await _configLoader.LoadConfigsAsync();
            if (_modbusDevices.Count == 0)
                _logger.LogWarning("未加载到任何设备配置，轮询循环将空转（可通过重载配置恢复采集）");

            // 2.根据设备配置获取设备从站COMs,根据COMs初始化_portPool
            var slaveCOMs = _modbusDevices.Select(d => d.SlaveCOM);
            _portManager.InitializePorts(slaveCOMs);

            // 3.轮询所设备
            IsRunning = true;
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await PollAllDevicesAsync(stoppingToken);
                        LastPollTime = DateTime.UtcNow;   // 每轮轮询完成更新一次

                        // 采集周期从配置读取，运行时改配置重载后立即生效
                        var pollIntervalMs = _systemConfigService.GetValueInt("Modbus.PollIntervalMs", 1000);
                        await Task.Delay(pollIntervalMs, stoppingToken);   // 带 token：停机时立即醒来抛取消异常（须在 try 内有 catch 接），否则傻等满间隔
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Modbus 轮询异常");
                    }
                }
            }
            finally
            {
                IsRunning = false;
                _logger.LogInformation("Modbus 轮询循环已退出");
            }
        }

        /// <summary>并行编排：按主站口分组，跨口并行（不同总线互不干扰）、同口设备串行（RS485 半双工，一问一答）</summary>
        private async Task PollAllDevicesAsync(CancellationToken token)
        {

            if (_modbusDevices == null || _modbusDevices.Count == 0)
                return;

            // 按主站口分组：GroupBy 时键值对已成形（Key=查映射算出的主站COM，组员=挂在该口下的设备）
            // 结果形如 { "COM1": [设备1, 设备2], "COM3": [设备3, 设备4] }
            var devicesByCOM = _modbusDevices
                .GroupBy(d => GetMasterCOM(d.SlaveCOM))
                .Where(g => g.Key != null)
                .ToDictionary(g => g.Key!, g => g.ToList());

            // async: COM1和COM3并行执行
            // foreach: COM下的设备串行执行
            var portTasks = devicesByCOM.Select(async kvp =>
            {
                var (masterCOM, modbusDevices) = (kvp.Key, kvp.Value);

                // 获取port => 创建master
                var port = _portManager.GetPort(masterCOM);
                if (port == null)
                {
                    _logger.LogWarning("主站端口 {COM} 不可用", masterCOM);
                    return;
                }
                var master = _portManager.GetOrCreateMaster(port);

                // 设备之间串行执行
                foreach (var device in modbusDevices)
                {
                    if (token.IsCancellationRequested)
                        break;
                    await _dataReader.PollDeviceAsync(master, device, token);
                }
            });

            await Task.WhenAll(portTasks);
        }

        // 从站口 → 主站口（走 PortManager 缓存）
        private string? GetMasterCOM(string slaveCOM)
            // 走 PortManager 内的映射缓存，避免每轮采集重复反序列化 JSON
            => _portManager.GetMasterComBySlaveCom(slaveCOM);

        /// <summary>Host 停机时自动调用：先释放资源，再等循环退出</summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Modbus 采集服务正在停止...");
            ReleaseResources();
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("Modbus 采集服务已停止");
        }

        /// <summary>对外数据出口：返回缓存最新值快照（实时接口/健康检查用）</summary>
        public Dictionary<int, Dictionary<int, double>> GetCurrentData()
            => _cache.GetAllData();

        /// <summary>API 触发的配置重载：释放通信资源 → 重新查库 → 重开串口（循环不停，下轮用新配置）</summary>
        public async Task ReloadConfigsAsync()
        {
            _logger.LogInformation("重新加载设备配置...");
            ReleaseResources();
            _modbusDevices = await _configLoader.LoadConfigsAsync();
            if (_modbusDevices.Count > 0)
            {
                var slaveCOMs = _modbusDevices.Select(d => d.SlaveCOM);
                _portManager.InitializePorts(slaveCOMs);
            }
        }

        // 释放串口池与 Master 池（PortManager.Dispose 统一处理，停机/重载共用）
        private void ReleaseResources()
            => _portManager.Dispose();
    }
}