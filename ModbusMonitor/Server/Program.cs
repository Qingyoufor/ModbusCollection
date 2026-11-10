using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using Server.Background;
using Server.Infrastructure.Data;
using Server.Infrastructure.Mappings;
using Server.Infrastructure.Middleware;
using Server.Infrastructure.ModbusRTU;
using Server.Infrastructure.Repositories;
using Server.Services;
using System.Text;

namespace Server
{
    public class Program
    {
        public async static Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()                          // 只记录 Information 及以上
                // 压掉 EF Core 的 SQL 日志（Information 级），慢查询等警告仍可见
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.Console(theme: ConsoleTheme.None)           // 控制台输出（开发调试）
                .WriteTo.File("logs/log-.txt",                       // 写到 logs 目录，文件按天滚动
                    rollingInterval: RollingInterval.Day,            // log-20260809.txt
                    retainedFileCountLimit: 14,                      // 最多保留 14 个文件
                    shared:true,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            var builder = WebApplication.CreateBuilder(args);

            // builder.Services.AddXxx() → 注册阶段，往容器里放服务
            // builder.Build()        → 构建阶段，生成 IServiceProvider
            // app.UseXxx() / MapXxx() → 使用阶段，从容器取出服务
            //dfbjkds

            // ====== EF Core DbContext ======
            builder.Services.AddDbContext<ModbusMonitorDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // ====== CORS ======
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowClient", policy =>
                {
                    policy.WithOrigins("http://localhost:5000")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });

            // ====== Controller ======
            builder.Services.AddControllers();

            // ====== Swagger =====
            builder.Services.AddEndpointsApiExplorer(); // SwaggerUI的数据源
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "ModbusMonitor API", Version = "v1" });
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header. Example: \"Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
            });

            // ====== JWT 认证 ======
            var jwtSettings = builder.Configuration.GetSection("Jwt");

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }) // 走JWT认证
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new()
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["key"]!)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            });

            // ====== AutoMapper ======
            // AddMaps：扫描 MappingProfile 所在程序集，自动发现并加载所有继承 Profile 的映射配置
            // 同时把 IMapper 注册进 DI 容器，Service 构造函数声明 IMapper 即可注入
            builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(MappingProfile).Assembly));

            // ====== 授权 ======
            builder.Services.AddAuthorization();

            // 把 ASP.NET Core 的 ILogger 全部重定向到 Serilog
            builder.Host.UseSerilog();

            // ====== DI 登记：Repository（Scoped，随请求生命周期） ======
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
            builder.Services.AddScoped<IDataPointRepository, DataPointRepository>();
            builder.Services.AddScoped<IAlarmRepository, AlarmRepository>();
            builder.Services.AddScoped<IDeviceDataRepository, DeviceDataRepository>();

            // ====== DI 登记：Service（业务服务，Scoped）   ======
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IDeviceService, DeviceService>();
            builder.Services.AddScoped<IDataPointService, DataPointService>();
            builder.Services.AddScoped<IRealtimeService, RealtimeService>();
            builder.Services.AddScoped<IAlarmService, AlarmService>();
            builder.Services.AddScoped<IHistoryService, HistoryService>();

            // ====== DI 登记：Service（全局单例：配置缓存 / 系统管理） ======
            builder.Services.AddSingleton<ISystemConfigService, SystemConfigService>();
            builder.Services.AddSingleton<ISystemService, SystemService>();


            // ========== Modbus 模块（采集引擎，Singleton：缓存/配置/端口/读取器 + 对外查询接口） ==========
            builder.Services.AddSingleton<ModbusDataCache>();
            builder.Services.AddSingleton<ModbusConfigLoader>();
            builder.Services.AddSingleton<ModbusPortManager>();
            builder.Services.AddSingleton<ModbusDataReader>();
            // 采集循环与对外查询必须是同一个实例：先注册自身，再让接口和 HostedService 都指向它
            builder.Services.AddSingleton<ModbusService>();
            builder.Services.AddSingleton<IModbusService>(sp => sp.GetRequiredService<ModbusService>());

            // ========== 后台服务（HostedService：随 Host 启动自转，无需外部调用） ==========
            builder.Services.AddHostedService(sp => sp.GetRequiredService<ModbusService>());  // Modbus 采集轮询
            builder.Services.AddHostedService<DataArchiveService>();     // 数据归档（5 秒快照落库）
            builder.Services.AddHostedService<AlarmDetectionService>();   // 报警检测（1 秒比对阈值）

            

            // ============================
            //          构建应用
            // ============================
            var app = builder.Build();

            // ====== 中间件管道（顺序不能乱） ======
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ModbusMonitor API v1"));
            }
            
            app.UseMiddleware<ExceptionHandlingMiddleware>();//放在最前面
            app.UseCors("AllowClient");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            // ====== 启动初始化：加载 setting.json 进缓存（app.Run 前；文件缺失会抛异常，属预期——提示手工创建） ======
            var configService = app.Services.GetRequiredService<ISystemConfigService>();
            await configService.LoadAsync();    // 文件内容全量加载进内存缓存

            app.Run();
        }
    }
}
