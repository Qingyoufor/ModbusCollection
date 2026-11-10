using Server.Infrastructure.ModbusRTU.Models;
using Server.Infrastructure.Repositories;
using Server.Models.Common;
using Server.Models.Entities;

namespace Server.Infrastructure.ModbusRTU
{
    /// <summary>设备配置加载器，返回 ModbusDevice 采集设备清单</summary>
    public class ModbusConfigLoader
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ModbusConfigLoader> _logger;

        public ModbusConfigLoader(IServiceScopeFactory scopeFactory, ILogger<ModbusConfigLoader> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>从数据库加载设备配置和测点信息</summary>
        public async Task<List<ModbusDevice>> LoadConfigsAsync()
        {
            // scope小容器，在跳出方法时释放
            using var scope = _scopeFactory.CreateScope();
            var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
            var dataPointRepo = scope.ServiceProvider.GetRequiredService<IDataPointRepository>();

            var devices = await deviceRepo.GetAllAsync();
            var onlineDevices = devices.ToList();

            if (onlineDevices.Count == 0)
            {
                _logger.LogWarning("没有配置任何设备");
                return new List<ModbusDevice>();
            }

            var modbusDevices = new List<ModbusDevice>();

            // 一次性加载全部测点，内存中按设备分组，避免逐台查询（N+1 → 2 条 SQL）
            var allPoints = await dataPointRepo.GetAllAsync();
            var pointsByDevice = allPoints.GroupBy(p => p.DeviceId)
                                          .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var device in onlineDevices)
            {
                pointsByDevice.TryGetValue(device.Id, out var points);
                points ??= new List<DataPoint>();
                modbusDevices.Add(new ModbusDevice
                {
                    DeviceId = device.Id,
                    Name = device.Name,
                    SlaveAddress = device.SlaveAddress,
                    SlaveCOM = device.ComPort,
                    DataPoints = points.Select(p => new DataPointConfig
                    {
                        DataPointId = p.Id,
                        Name = p.Name,
                        RegisterAddress = ConvertToProtocolAddress(p.RegisterAddress, p.RegisterType),
                        RegisterType = p.RegisterType,
                        DataType = p.DataType,
                        ScaleFactor = p.ScaleFactor,
                        DecimalPlaces = p.DecimalPlaces,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue
                    }).OrderBy(p => p.RegisterAddress).ToList()
                });
            }

            _logger.LogInformation("已加载 {DeviceCount} 个设备，共 {PointCount} 个测点",
                modbusDevices.Count,
                modbusDevices.Sum(d => d.DataPoints.Count));

            return modbusDevices;
        }

        /// <summary>将 PLC 地址转换为协议地址（40001→0, 30001→0）</summary>
        private static int ConvertToProtocolAddress(int registerAddress, RegisterType type)
            => type == RegisterType.HoldingRegister? registerAddress - 40001 : registerAddress - 30001;
    }
}