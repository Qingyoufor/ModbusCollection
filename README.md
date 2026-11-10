# ModbusMonitor

一个面向工业现场的**设备数据采集与监控系统**（SCADA 简化版），通过 Modbus RTU 协议从 PLC/仪器采集实时数据，提供实时监控、报警检测、历史趋势、系统配置等功能。包含 ASP.NET Core Web API 后端与 WPF 桌面客户端。

## 功能特性

- **Modbus RTU 采集**：轮询多个从站设备，按寄存器区间合并读取，支持字节序、缩放系数、小数位解析
- **实时监控**：设备树导航 + 实时数值面板 + LiveCharts2 实时趋势曲线
- **报警系统**：高限/低限越限检测，报警去重与确认
- **历史数据**：定期归档实时值入库，支持按设备/测点/时间段查询趋势，CSV 导出
- **系统设置**：串口参数、端口映射、采集周期、数据保留天数（setting.json 配置，运行时可改热生效）
- **认证**：JWT + 刷新令牌，前端 401 自动无感续期
- **日志**：Serilog 结构化日志，控制台 + 按天滚动文件

## 技术栈

| 层 | 技术 |
|---|---|
| 后端 | .NET 10 / ASP.NET Core Web API、EF Core (SqlServer)、AutoMapper、NModbus、Serilog、Swagger |
| 前端 | WPF、Prism (DryIoc)、HandyControl、LiveCharts2、Microsoft.Xaml.Behaviors |
| 数据 | SQL Server（Code-First 迁移）、Modbus RTU（虚拟串口 com0com/ELTIMA 调试）|

## 架构概览

```
后端 Server（Repository → Service → Controller + 后台服务）

  setting.json ──> SystemConfigService(内存缓存)
                          │
  [Modbus RTU 从站] ──> ModbusService(轮询) ──> ModbusDataCache(实时值)
                          │                        │
                                                    ├─> Realtime API   ──> 实时监控页
                          ├─ AlarmDetectionService  └─> Alarm 表        ──> 报警中心
                          └─ DataArchiveService ──> DeviceData 历史表 ──> 历史趋势 + CSV
```

```
前端 Client（Prism 模块化 + 严格 MVVM）

  ShellView(左侧菜单 + Right Region)
    ├─ 登录页
    ├─ 设备管理页
    ├─ 实时监控页（设备树 + 数值面板 + 趋势曲线）
    ├─ 报警中心
    ├─ 历史趋势页
    └─ 系统设置页（Modbus 配置含数据保留、关于）
```

## 目录结构

```
ModbusMonitor/
├── Client/                        # WPF 桌面客户端
│   ├── Infrastructure/            #   HttpClientBase / TokenStore
│   ├── Models/                    #   模型与 DTO
│   ├── Modules/                   #   Prism 模块（设备管理等）
│   ├── Service/                   #   前端服务层
│   ├── ViewModels/                #   ViewModel
│   └── Views/                     #   XAML 视图
└── Server/                        # ASP.NET Core 后端
    ├── Background/                #   后台服务（数据归档、报警检测）
    ├── Infrastructure/
    │   ├── Auth/                  #   JWT 签发
    │   ├── Controllers/           #   API 控制器
    │   ├── Data/                  #   DbContext
    │   ├── Mappings/              #   AutoMapper 映射
    │   ├── Middleware/            #   全局异常处理
    │   ├── ModbusRTU/             #   Modbus 采集（端口/分组/解析/缓存）
    │   └── Repositories/          #   仓储层
    ├── Migrations/                #   EF Core 迁移
    ├── Models/                    #   实体 / DTO / 枚举
    ├── Services/                  #   业务逻辑层
    ├── Program.cs                 #   入口与 DI
    └── setting.json               #   系统运行时配置
```

## 运行前提

1. **SQL Server**：可用实例，将连接字符串配置到 Server 的 `appsettings.Development.json`（数据库名 `modbus_monitor`）。
2. **数据库**：首次运行执行迁移建库（`dotnet ef database update`）。
3. **串口**：采集需要真实串口或虚拟串口对（com0com/ELTIMA 建 COM1⇄COM2）。
4. **setting.json**：系统运行时配置（已在项目内提供模板），若缺文件需手工按模板创建。

## 启动步骤

### 后端 Server
```bash
cd ModbusMonitor/Server
dotnet run
# Swagger: http://localhost:5078/swagger
```
> 默认监听 `http://localhost:5078/`，与客户端 `HttpClientBase` 的 BaseUrl 一致。

### 客户端 Client
```bash
cd ModbusMonitor/Client
dotnet run
```

### 初始化数据
首次运行后，通过 API 创建设备 / 测点，配置好设备的串口 COM 与从站地址。

## 系统配置（setting.json）

所有 Modbus 运行参数与数据保留天数集中在此，键用点分路径访问，与嵌套结构一一对应：

```json
{
  "Modbus": {
    "PortMappings": { "COM1": "COM2", "COM3": "COM4" },
    "BaudRate": 9600,
    "PollIntervalMs": 1000
  },
  "DataRetention": { "DeviceDataDays": 30, "AlarmDays": 30 }
}
```

- 修改后通过系统设置页"保存"或重启服务生效。
- 配置在内存缓存中扁平化为点分键（`Modbus.BaudRate`），读取走缓存、写入直接写回文件。

## 主要 API

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/auth/signIn` | 登录，签发 Token 对 |
| POST | `/api/auth/refresh` | 刷新令牌 |
| POST | `/api/auth/logout` | 登出 |
| GET/POST/PUT/DELETE | `/api/devices`、`/api/dataPoints` | 基础 CRUD |
| GET | `/api/realtime/devices/all` | 全量实时数据（含报警状态） |
| GET | `/api/alarms/current`、`/api/alarms/history` | 当前报警 / 历史报警（分页、筛选） |
| PUT | `/api/alarms/{id}/acknowledge` | 确认报警 |
| GET | `/api/history/data`、`/api/history/statistics`、`/api/history/export` | 趋势 / 统计 / CSV 导出 |
| GET/PUT | `/api/system/settings` | 读取/保存系统配置 |
| POST | `/api/system/modbus/reload` | 手动重载 Modbus |
| GET | `/api/system/info` | 系统运行信息 |

统一返回格式：`{ "Code": 200, "Message": "...", "Data": ... }`。

## 配置说明与注意事项

- **端口映射方向**：setting.json 中 `PortMappings` 为「从站 → 主站」，采集时按设备的从站端口查出对应主站端口并打开串口。
- **地址换算**：数据库寄存器地址使用 PLC 绝对地址（Holding Register 40001 起），采集时自动换算为协议偏移。
- **后台服务与作用域**：后台服务为单例，访问仓储时用 `IServiceScopeFactory` 手动建作用域，避免 DbContext 并发问题。
