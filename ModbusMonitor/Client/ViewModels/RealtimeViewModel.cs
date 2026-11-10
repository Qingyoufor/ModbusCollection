using Client.Models;
using Client.Models.Dtos;
using Client.Service;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Client.ViewModels
{
    public partial class RealtimeViewModel : BindableBase, INavigationAware
    {
        // ======== 服务注入 ========
        private readonly IRealtimeService _realtimeService;

        // ======== 设备分组（按串口） ========
        private ObservableCollection<DeviceGroupNode> _deviceGroupNodes = new();
        private int _selectedDeviceId;

        // ======== 设备数据相关 ========
        private string _selectedDeviceName = string.Empty;
        private bool _isDeviceSelected;

        // ======== 并发/时序控制 ========
        private bool _isLoading;             // 有请求在途：tick 直接跳过，避免重复追加
        private int _deviceGeneration;       // 设备代际号：切换设备后丢弃在途的旧响应
        private bool _pendingTrendInit;      // 切换设备时首帧被跳过，仍需初始化趋势图

        // ======== 定时器 ========
        private DispatcherTimer? _timer;

        // ======== 命令 ========
        public DelegateCommand<object?> SelectedItemChangedCommand { get; }

        // ======== 构造函数 ========
        public RealtimeViewModel(IRealtimeService realtimeService)
        {
            _realtimeService = realtimeService;
            SelectedItemChangedCommand = new DelegateCommand<object?>(OnSelectedItemChanged);

            _highThresholdSeries = CreateThresholdSeries(_highThresholdPoints, SKColors.OrangeRed);
            _lowThresholdSeries = CreateThresholdSeries(_lowThresholdPoints, SKColors.Orange);
            _highThresholdSeries.IsVisible = false;
            _lowThresholdSeries.IsVisible = false;

            // 初始化趋势图轴配置（Y 轴范围随后按数据自适应）
            TrendYAxes = new Axis[] { CreateValueAxis(0, 100) };

            // DateTimeAxis 是 LiveCharts 的时间轴：每 10 秒一个刻度，标签平放不斜排
            _trendXAxis = new DateTimeAxis(TimeSpan.FromSeconds(10), t => t.ToString("mm:ss"))
            {
                TextSize = AxisTextSize,
                LabelsPaint = AxisLabelsPaint,
                // 竖向网格线在实时曲线里是噪音，直接不画
                ShowSeparatorLines = false,
                // 不额外往两边让出空白，数据从轴起点开始
                BouncingDistance = 0,
                // 刻度从轴起点（第一个数据点）开始，每 10 秒一个，而不是落在“整 10 秒”上
                MinStep = TimeSpan.FromSeconds(10).Ticks,
                ForceStepToMin = true
            };
            TrendXAxes = new Axis[] { _trendXAxis };
        }

        // ======== 数据加载方法 ========

        /// <summary>加载全部设备数据，按串口分组组装设备树</summary>
        private async Task LoadTreeAsync()
        {
            var allDeviceData = await _realtimeService.GetAllDataAsync();
            if (allDeviceData == null)
                return;

            DeviceGroupNodes = new ObservableCollection<DeviceGroupNode>(
                allDeviceData.Devices
                    .GroupBy(d => string.IsNullOrWhiteSpace(d.ComPort) ? "未配置串口" : d.ComPort)
                    .OrderBy(g => g.Key)
                    .Select(g => new DeviceGroupNode
                    {
                        Name = g.Key,
                        Devices = g.Select(d => new DeviceTreeNode
                        {
                            Id = d.DeviceId,
                            Name = d.DeviceName
                        }).ToList()
                    }));
        }

        /// <summary>
        /// 加载选中设备的实时数据
        /// </summary>
        /// <param name="initializeTrend">首次加载需初始化趋势图配置</param>
        private async Task LoadDeviceDataAsync(bool initializeTrend = false)
        {
            // 上一次请求还在途时直接跳过，避免同一秒重复追加趋势点（60 秒窗口被压缩）
            if (_isLoading)
                return;

            _isLoading = true;
            try
            {
                int generation = _deviceGeneration;
                int deviceId = _selectedDeviceId;

                var device = await _realtimeService.GetDeviceDataAsync(deviceId);

                // 请求期间用户又切了设备：丢弃这台设备的过期响应，避免数据/标题串台
                if (device == null || generation != _deviceGeneration)
                    return;

                SelectedDeviceName = device.DeviceName;
                UpdateDataPoints(device);

                // 首次选中设备时初始化趋势图（只执行一次）；放在数值填充之后，测点名和标题才取得到
                bool needInit = initializeTrend || _pendingTrendInit;
                if (needInit)
                {
                    _pendingTrendInit = false;
                    InitializeTrendChart(device);
                }

                EnsureSelectedPoint();          // 没选中或选中项已被删除时回落到第一个测点

                UpdateTrendCollections(device);
                UpdateTrendXAxis();
                UpdateTrendYAxis();
                UpdateThresholds();
            }
            finally
            {
                _isLoading = false;
            }

            EnsureTimerStarted(); // 保证 1 秒轮询定时器运行
        }

        // ======== INavigationAware（Prism 导航生命周期）========

        /// <summary>返回 true：重用当前视图实例，保持设备树状态</summary>
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        /// <summary>离开页面时不停止定时器：让实时数据与图线在后台持续刷新，切回时无缝衔接</summary>
        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // 定时器继续运行，保持后台轮询（IsNavigationTarget=true 保证本实例被缓存、不会销毁）
        }

        /// <summary>进入页面时：首次加载设备树 + 启动定时器</summary>
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (DeviceGroupNodes.Count == 0)
            {
                _ = LoadTreeAsync();
            }
            EnsureTimerStarted();
        }

        /// <summary>定时器回调：每 1 秒刷新设备数据和趋势图（不重新初始化）</summary>
        private async void OnRefreshTick(object? sender, EventArgs e)
        {
            try
            {
                await LoadDeviceDataAsync(initializeTrend: false);
            }
            catch (Exception ex)
            {
                // async void 里的异常会直接终结进程，这里兜住并记录
                System.Diagnostics.Debug.WriteLine($"实时刷新失败: {ex}");
            }
        }

        // ======== 定时器方法 ========

        /// <summary>启动 1 秒轮询定时器（已有则不再新建）</summary>
        private void EnsureTimerStarted()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += OnRefreshTick;
                _timer.Start();
            }
        }

        // ======== 属性 ========

        public ObservableCollection<DeviceGroupNode> DeviceGroupNodes
        {
            get => _deviceGroupNodes;
            set => SetProperty(ref _deviceGroupNodes, value);
        }

        public string SelectedDeviceName
        {
            get => _selectedDeviceName;
            set => SetProperty(ref _selectedDeviceName, value);
        }


        // 反向属性：通过 !IsDeviceSelected 计算，不需要自定义转换器
        public bool IsDeviceNotSelected => !IsDeviceSelected;

        /// <summary>是否选中了设备，选中时显示数值面板和趋势图，否则显示占位文本</summary>
        public bool IsDeviceSelected
        {
            get => _isDeviceSelected;
            set
            {
                if (SetProperty(ref _isDeviceSelected, value))
                {
                    RaisePropertyChanged(nameof(IsDeviceNotSelected));
                }
            }
        }
    }
}
