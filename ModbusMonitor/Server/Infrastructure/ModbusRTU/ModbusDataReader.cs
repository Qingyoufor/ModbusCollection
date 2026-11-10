using NModbus;
using Server.Infrastructure.ModbusRTU.Models;
using Server.Models.Common;
// NModbus.Device 里也有 ModbusDevice，用别名消歧，指向本项目的采集设备模型
using ModbusDevice = Server.Infrastructure.ModbusRTU.Models.ModbusDevice;

namespace Server.Infrastructure.ModbusRTU
{
    /// <summary>
    /// Modbus 数据读取器
    /// 负责执行实际的 Modbus RTU 通信，读取寄存器数据并写入缓存
    /// </summary>
    public class ModbusDataReader
    {
        private readonly ILogger<ModbusDataReader> _logger;
        private readonly ModbusDataCache _cache;

        public ModbusDataReader(ILogger<ModbusDataReader> logger, ModbusDataCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// 轮询单个设备：读取所有测点数据
        /// </summary>
        public async Task PollDeviceAsync(IModbusSerialMaster master, ModbusDevice modbusDevice, CancellationToken token)
        {
            try
            { 
                // 1. 测点分组：将连续地址的测点合并，减少通信次数
                var groups = ModbusGroupingHelper.GroupContinousRegister(modbusDevice.DataPoints);
                if (groups.Count == 0)
                    return;  // 无测点则直接返回

                // 2. 逐组读取
                foreach (var group in groups)
                {
                    // 检查取消请求（应用退出时立即停止）
                    if (token.IsCancellationRequested)
                        break;

                    try
                    {
                        // 带重试机制的读取
                        await ReadGroupWithRetryAsync(master, modbusDevice, group, token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // 单组失败不影响其他组，记录日志继续执行
                        _logger.LogError(ex, "设备 {Device} 分组读取失败 [起始:{Start} 长度:{Length}]",
                            modbusDevice.Name, group.StartAddress, group.GroupLength);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 整个设备轮询失败，记录日志（下一个设备继续）
                _logger.LogError(ex, "采集设备 {Device} (从站:{Address}) 失败",
                    modbusDevice.Name, modbusDevice.SlaveAddress);
            }
        }

        /// <summary>
        /// 带重试机制的寄存器读取
        /// </summary>
        private async Task ReadGroupWithRetryAsync(
            IModbusSerialMaster master,
            ModbusDevice modbusDevice,
            RegisterReadGroup group,
            CancellationToken token,
            int maxRetries = 2)
        {
            ushort[]? registers = null;   // 读取到的原始寄存器值
            Exception? lastException = null;

            // 重试循环
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    // 尝试读取寄存器
                    registers = await ReadRegistersAsync(master, modbusDevice.SlaveAddress, group);
                    break;  // 成功则跳出循环
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastException = ex;

                    // 如果不是最后一次重试，记录警告并等待后继续
                    if (retry < maxRetries - 1)
                    {
                        _logger.LogWarning("设备 {Device} 读取失败，第 {Retry} 次重试", modbusDevice.Name, retry + 1);
                        await Task.Delay(50 * (retry + 1), token);  // 延递增：50ms → 100ms
                    }
                }
            }

            // 重试全部失败，抛出异常
            if (registers == null)
            {
                throw new Exception($"重试 {maxRetries} 次后仍失败", lastException);
            }

            // 成功读取，解析并写入缓存
            UpdateCacheFromGroup(modbusDevice, group, registers);
        }

        /// <summary>
        /// 唯一发帧收帧的通信点：RegisterType 翻译成功能码（Holding→03/Input→04）。
        /// 返回已剥掉地址/功能码/CRC 的裸数据，如响应帧 [01 03 04 43 48 00 00 CRC] → { 0x4348, 0x0000 }
        /// </summary>
        private async Task<ushort[]> ReadRegistersAsync(IModbusSerialMaster master, byte slaveAddress, RegisterReadGroup group)
        {
            return group.RegisterType == RegisterType.HoldingRegister
                ? await master.ReadHoldingRegistersAsync(slaveAddress, (ushort)group.StartAddress, (ushort)group.GroupLength)
                : await master.ReadInputRegistersAsync(slaveAddress, (ushort)group.StartAddress, (ushort)group.GroupLength);
        }

        /// <summary>
        /// 解析寄存器原始值并写入缓存
        /// </summary>
        private void UpdateCacheFromGroup(ModbusDevice modbusDevice, RegisterReadGroup group, ushort[] registers)
        {
            foreach (var point in group.DataPoints)
            {
                try
                {
                    // 1. 计算测点在寄存器数组中的偏移量
                    int offset = point.RegisterAddress - group.StartAddress;

                    // 2. 根据数据类型确定需要的寄存器数量
                    int registerCount = point.RegisterLength;

                    // 3. 提取测点对应的寄存器片段
                    var pointRegisters = new ushort[registerCount];
                    Array.Copy(registers, offset, pointRegisters, 0, registerCount);

                    // 4. 调用解析器，将原始值转换为工程值
                    double value = ModbusDataParser.ParseRawValue(pointRegisters, point);

                    // 5. 范围校验（超出 MinValue~MaxValue 时记录警告）
                    if (!ModbusDataParser.IsInRange(value, point))
                    {
                        _logger.LogWarning("测点 {Point} 值 {Value} 超出范围 [{Min}, {Max}]",
                            point.Name, value, point.MinValue, point.MaxValue);
                    }

                    // 6. 写入缓存（线程安全）
                    _cache.UpdateDataPoint(modbusDevice.DeviceId, point.DataPointId, value);

                    // 7. 输出 Debug 日志（方便调试）
                    _logger.LogDebug("设备 {Device} → {Point} = {Value}",
                        modbusDevice.Name, point.Name, value);
                }
                catch (Exception ex)
                {
                    // 单个测点解析失败不影响其他测点，记录日志继续执行
                    _logger.LogError(ex, "解析测点 {Point} 失败", point.Name);
                }
            }
        }
    }
}
