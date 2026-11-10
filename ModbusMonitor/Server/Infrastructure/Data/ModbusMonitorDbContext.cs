using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Server.Models.Common;
using Server.Models.Entities;

namespace Server.Infrastructure.Data
{
    //EF Core 对于返回实体（_dbSet注册的实体）的操作，会对这个实体里的所有映射到数据路的列属性进行追踪，不用手动Update()
    public class ModbusMonitorDbContext : DbContext
    {
        //使用program.cs里ModbusMonitorDbContext配置到好的options
        public ModbusMonitorDbContext(DbContextOptions<ModbusMonitorDbContext> options) : base(options) { }

        //通过Set<T>获取实例操作数据库
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<DataPoint> DataPoints => Set<DataPoint>();
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<DeviceData> DeviceDatas => Set<DeviceData>();
        public DbSet<Alarm> Alarms => Set<Alarm>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //设置没有声明长度的string类型字段的最大长度为256
            //GetEntityTypes()返回所有已注册的Set<T>
            foreach (var entity in modelBuilder.Model.GetEntityTypes())   
            {
                foreach (var prop in entity.GetProperties().Where(p =>p.ClrType == typeof(string)))
                {
                    if(!prop.GetMaxLength().HasValue && !prop.IsPrimaryKey())
                    {
                        prop.SetMaxLength(256);
                    }
                }
            }

            //枚举以字符串形式存库：直接看表就是 HoldingRegister / Int16，C# 侧仍按枚举使用
            modelBuilder.Entity<DataPoint>()
                .Property(p => p.RegisterType)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<DataPoint>()
                .Property(p => p.DataType)
                .HasConversion<string>()
                .HasMaxLength(20);

            //解决级联删除的问题 Device是DataPoint和Alarm的父表，删除Device时会同时删除子表(EF 默认删除行为)
            //如果Device里有public ICollection<Alarm> Alarms { get; set; }，则WithMany(d => d.Alarms)

            modelBuilder.Entity<Alarm>()
                .HasOne(a => a.Device)
                .WithMany()
                .HasForeignKey(a => a.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Alarm>()
                .HasOne(a => a.DataPoint)
                .WithMany()
                .HasForeignKey(a => a.DataPointId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DeviceData>()
                .HasOne(d => d.Device)
                .WithMany()
                .HasForeignKey(d => d.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DeviceData>()
                .HasOne(d => d.DataPoint)
                .WithMany()
                .HasForeignKey(d => d.DataPointId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DataPoint>()
                .HasOne(d => d.Device)
                .WithMany(d => d.DataPoints)
                .HasForeignKey(d => d.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            //设备编码唯一（应用层 CodeExistsAsync 只做友好提示，真正的约束由数据库保证）
            modelBuilder.Entity<Device>()
                .HasIndex(d => d.Code)
                .IsUnique();

            //添加复合索引(A,B,C)（优化对于数据量大的表的查询速度 A -> B -> C，最后返回按C排序的数据）
            modelBuilder.Entity<DeviceData>()
                .HasIndex(d => new { d.DeviceId, d.DataPointId, d.RecordedAt })
                .HasDatabaseName("IX_DeviceDatas_Device_Point_Time");

            modelBuilder.Entity<Alarm>()
                .HasIndex(a => new { a.DeviceId, a.Status, a.OccurredAt })
                .HasDatabaseName("IX_Alarms_Device_Status_Time");



            //种子数据
            var seedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = "$2a$11$fgGNAdEqb6qn8FOOJt4E2OgRzmqqdz/lNMZiInsJ1oYp5IY0Umeqe",
                    IsActive = true,
                    CreatedAt = seedTime
                }
            );
        }
    }
}
