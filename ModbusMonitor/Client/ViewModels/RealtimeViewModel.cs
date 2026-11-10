using Client.Models;
using Client.Models.Dtos;
using Client.Service;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Client.ViewModels
{
    public class RealtimeViewModel : BindableBase, INavigationAware
    {
        // ======== 服务注入 ========
        private readonly IRealtimeService _realtimeService;

        // ======== 设备分组（按串口） ========
        private ObservableCollection<DeviceGroupNode> _deviceGroupNodes = new();
        private int _selectedDeviceId;

        // ======== 设备数据相关 ========
        private string _selectedDeviceName = string.Empty;
        private bool _isDeviceSelected;
        // 主集合：保持服务端返回顺序，每秒就地更新同一个对象
        private readonly ObservableCollection<DataPointDisplayModel> _dataPoints = new();
        // 界面渲染集合：经搜索/仅看报警筛选并报警置顶排序；内容无变化时不重建
        private readonly ObservableCollection<DataPointDisplayModel> _viewPoints = new();
        private string _searchText = string.Empty;
        private bool _onlyAlarms;
        private DataPointDisplayModel? _selectedPoint;

        // ======== 并发/时序控制 ========
        private bool _isLoading;             // 有请求在途：tick 直接跳过，避免重复追加
        private int _deviceGeneration;       // 设备代际号：切换设备后丢弃在途的旧响应
        private bool _pendingTrendInit;      // 切换设备时首帧被跳过，仍需初始化趋势图

        // ======== 趋势图相关 ========
        private const int MaxTrendPoints = 60;   // 1 秒 1 点 × 60 = 60 秒滚动窗口
        // 每个测点存带时间戳的点（DateTimePoint），固定 60 秒滚动窗口
        private readonly Dictionary<int, ObservableCollection<DateTimePoint>> _trendDataCollections = new();
        private int _selectedTrendPointId = -1;
        private ISeries[] _trendSeries = Array.Empty<ISeries>();
        private Axis[] _trendYAxes;
        private Axis[] _trendXAxes = Array.Empty<Axis>();
        private string _trendTitle = string.Empty;

        // 报警阈值虚线：用数据窗口两端各一个点画成水平虚线
        private readonly ObservableCollection<DateTimePoint> _highThresholdPoints = new();
        private readonly ObservableCollection<DateTimePoint> _lowThresholdPoints = new();
        private readonly LineSeries<DateTimePoint> _highThresholdSeries;
        private readonly LineSeries<DateTimePoint> _lowThresholdSeries;

        // Y 轴当前范围缓存：只有范围变化时才重建轴，避免每秒刷新时刻度上下抖
        private double _axisMin = double.NaN;
        private double _axisMax = double.NaN;

        // X 轴：固定一个实例，只改 Min/MaxLimit，让刻度从“第一个数据点”开始，和数据起点对齐
        private readonly Axis _trendXAxis;
        private long _axisXMin = long.MinValue;
        private long _axisXMax = long.MinValue;

        /// <summary>趋势曲线颜色</summary>
        private static readonly SKColor TrendColor = SKColor.Parse("#3B82F6");

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
                // 轴标签字号：X/Y 统一 18
                TextSize = 18,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#8C8C8C")),
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

        /// <summary>创建一条报警阈值虚线：两个数据点连成水平线，不响应鼠标</summary>
        private static LineSeries<DateTimePoint> CreateThresholdSeries(
            ObservableCollection<DateTimePoint> points, SKColor color) =>
            new()
            {
                Values = points,
                Stroke = new SolidColorPaint(color)
                {
                    StrokeThickness = 1,
                    PathEffect = new DashEffect(new float[] { 6, 4 }, 0)
                },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                IsHoverable = false
            };

        /// <summary>创建数值轴：淡灰网格线 + 灰字刻度</summary>
        private static Axis CreateValueAxis(double min, double max) =>
            new()
            {
                MinLimit = min,
                MaxLimit = max,
                // 轴标签字号：X/Y 统一 18
                TextSize = 18,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#8C8C8C")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#F0F0F0")) { StrokeThickness = 1 }
            };

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

                // 调用服务端实时数据 API
                var device = await _realtimeService.GetDeviceDataAsync(deviceId);

                // 请求期间用户又切了设备：丢弃这台设备的过期响应，避免数据/标题串台
                if (device == null || generation != _deviceGeneration)
                    return;

                SelectedDeviceName = device.DeviceName;
                UpdateDataPoints(device);       // 刷新数值面板（就地更新）

                // 首次选中设备时初始化趋势图（只执行一次）；放在数值填充之后，测点名和标题才取得到
                bool needInit = initializeTrend || _pendingTrendInit;
                if (needInit)
                {
                    _pendingTrendInit = false;
                    InitializeTrendChart(device);
                }

                EnsureSelectedPoint();          // 没选中或选中项已被删除时回落到第一个测点

                UpdateTrendCollections(device); // 追加趋势图数据点
                UpdateTrendXAxis();             // X 轴跟随数据区间，刻度从第一个点开始
                UpdateTrendYAxis();             // 按最新数据重算 Y 轴范围（范围没变则跳过重建）
                UpdateThresholds();             // 阈值虚线跟随数据窗口
            }
            finally
            {
                _isLoading = false;
            }

            EnsureTimerStarted(); // 保证 1 秒轮询定时器运行
        }

        /// <summary>首次选中设备时：为每个测点建趋势集合，并默认选中第一个测点</summary>
        private void InitializeTrendChart(RealtimeDeviceDataDto device)
        {
            if (_dataPoints.Count == 0)
                return;

            // 为每个测点创建独立的空数据集合，首个数据点由随后的 UpdateTrendCollections 统一追加
            _trendDataCollections.Clear();
            foreach (var point in _dataPoints)
            {
                _trendDataCollections[point.DataPointId] = new ObservableCollection<DateTimePoint>();
            }

            // 先清空选中，强制走一遍 setter 重建 Series（否则引用相同会被跳过）
            _selectedPoint = null;
            RaisePropertyChanged(nameof(SelectedPoint));
            SelectedPoint = _dataPoints[0]; // 由 setter 负责建 Series、X/Y 轴与阈值虚线
        }

        /// <summary>
        /// 就地同步测点：复用已有对象只改属性值，集合成员或顺序变化时才重排。
        /// 每秒重建整块 UI 会导致滚动位置与选中态丢失，也会让虚拟化失效。
        /// </summary>
        private void UpdateDataPoints(RealtimeDeviceDataDto device)
        {
            // 用索引器逐项写入：即使服务端返回重复 DataPointId 也不会抛异常
            var existing = new Dictionary<int, DataPointDisplayModel>();
            foreach (var vm in _dataPoints)
            {
                existing[vm.DataPointId] = vm;
            }

            var ordered = new List<DataPointDisplayModel>(device.DataPoints.Count);

            foreach (var p in device.DataPoints)
            {
                if (!existing.TryGetValue(p.DataPointId, out var vm))
                {
                    vm = new DataPointDisplayModel { DataPointId = p.DataPointId };
                }

                vm.Name = p.Name;
                vm.Value = p.Value;
                vm.Unit = p.Unit;
                vm.DecimalPlaces = p.DecimalPlaces;
                vm.AlarmStatus = p.AlarmStatus;
                vm.AlarmHigh = p.AlarmHigh;
                vm.AlarmLow = p.AlarmLow;

                ordered.Add(vm);
            }

            int before = _dataPoints.Count;

            // 成员或顺序变化（新增测点、服务端改排序）才重排主集合
            if (!ordered.SequenceEqual(_dataPoints))
            {
                _dataPoints.Clear();
                foreach (var vm in ordered)
                {
                    _dataPoints.Add(vm);
                }
            }

            RefreshViewPoints();

            if (_dataPoints.Count != before)
            {
                RaisePropertyChanged(nameof(IsCardMode));
                RaisePropertyChanged(nameof(IsTableMode));
            }
        }

        /// <summary>没有选中，或选中的测点已被服务端删除时，回落到第一个测点</summary>
        private void EnsureSelectedPoint()
        {
            if (_dataPoints.Count == 0)
            {
                // 测点被删空：显式清掉选中，避免曲线停在已不存在的测点上
                if (_selectedPoint != null)
                {
                    _selectedPoint = null;
                    RaisePropertyChanged(nameof(SelectedPoint));
                }
                return;
            }

            if (_selectedPoint == null || !_dataPoints.Contains(_selectedPoint))
            {
                SelectedPoint = _dataPoints[0];
            }
        }

        /// <summary>
        /// 按搜索词/仅看报警筛选并报警置顶排序。
        /// 用 Remove/Move/Insert 增量同步而不是 Clear+Add：后者会发出 Reset 通知，
        /// 导致表格/列表的选中高亮与滚动位置全部丢失。
        /// </summary>
        private void RefreshViewPoints()
        {
            string keyword = _searchText.Trim();

            var desired = _dataPoints
                .Where(p => !_onlyAlarms || p.IsAlarm)
                .Where(p => keyword.Length == 0 || p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.IsAlarm ? 0 : 1)   // 稳定排序：报警项置顶，组内保持服务端顺序
                .ToList();

            if (desired.Count == _viewPoints.Count && desired.SequenceEqual(_viewPoints))
                return;

            var keep = new HashSet<DataPointDisplayModel>(desired);

            // 先移除不该显示的
            for (int i = _viewPoints.Count - 1; i >= 0; i--)
            {
                if (!keep.Contains(_viewPoints[i]))
                {
                    _viewPoints.RemoveAt(i);
                }
            }

            // 再按目标顺序就位：已有的用 Move，缺的用 Insert
            for (int i = 0; i < desired.Count; i++)
            {
                var item = desired[i];
                if (i < _viewPoints.Count && ReferenceEquals(_viewPoints[i], item))
                    continue;

                int oldIndex = _viewPoints.IndexOf(item);
                if (oldIndex >= 0)
                    _viewPoints.Move(oldIndex, i);
                else
                    _viewPoints.Insert(i, item);
            }

            // 把选中项回推给控件：Move/Insert 本身会保留选中，
            // 但选中项被筛掉、再恢复显示时控件已经自行清空了选中状态，
            // 直接重发同一个值它不会认，必须先推 null 再推回原值
            if (_selectedPoint != null && keep.Contains(_selectedPoint))
            {
                var selected = _selectedPoint;
                _selectedPoint = null;
                RaisePropertyChanged(nameof(SelectedPoint));
                _selectedPoint = selected;
            }
            RaisePropertyChanged(nameof(SelectedPoint));
        }

        /// <summary>为所有测点追加新数值（滑动窗口：超出 MaxTrendPoints 时移除最早的数据点）</summary>
        private void UpdateTrendCollections(RealtimeDeviceDataDto device)
        {
            var now = DateTime.Now;

            foreach (var p in device.DataPoints)
            {
                // 服务端测点可能新增（如数据库加了测点后重启服务端）：字典缺键时就地创建，避免 KeyNotFoundException
                if (!_trendDataCollections.TryGetValue(p.DataPointId, out var collection))
                {
                    collection = new ObservableCollection<DateTimePoint>();
                    _trendDataCollections[p.DataPointId] = collection;
                }
                collection.Add(new DateTimePoint(now, p.Value));

                // 滑动窗口：保持最多 60 个点（1 秒 1 点 = 60 秒历史）
                if (collection.Count > MaxTrendPoints)
                {
                    collection.RemoveAt(0);
                }
            }
        }

        // ======== 趋势图方法 ========

        /// <summary>切换测点时重建 Series，绑定到对应测点的 ObservableCollection（不清空历史数据）</summary>
        private void UpdateTrendSeries()
        {
            if (_selectedPoint == null)
                return;

            // 服务端运行中新增的测点还没建集合时就地补一个，避免选中后曲线取不到数据
            if (!_trendDataCollections.TryGetValue(_selectedPoint.DataPointId, out var values))
            {
                values = new ObservableCollection<DateTimePoint>();
                _trendDataCollections[_selectedPoint.DataPointId] = values;
            }

            // 顺序即图层：阈值虚线放最底层，数据曲线画在最上层，阈值恰好等于当前值时
            // 也不会把曲线染成橙红色（各测点曲线颜色保持一致）
            TrendSeries = new ISeries[]
            {
                _highThresholdSeries,
                _lowThresholdSeries,
                new LineSeries<DateTimePoint>
                {
                    Values = values,
                    Name = _selectedPoint.Name,
                    Stroke = new SolidColorPaint(TrendColor) { StrokeThickness = 2 },
                    // 曲线下方淡蓝渐变，增强“趋势”观感
                    Fill = new LinearGradientPaint(
                        TrendColor.WithAlpha(70),
                        TrendColor.WithAlpha(0),
                        new SKPoint(0.5f, 0),
                        new SKPoint(0.5f, 1)),
                    // 纯曲线：不画数据点圆点
                    GeometrySize = 0,
                    LineSmoothness = 0.5
                }
            };

            UpdateTrendTitle(_selectedPoint);
        }

        /// <summary>X 轴锁定为 60 秒窗口，起点是第一个数据点：刚打开时曲线从左往右长，不会一下铺满整张图</summary>
        private void UpdateTrendXAxis()
        {
            if (_selectedTrendPointId == -1)
                return;

            if (!_trendDataCollections.TryGetValue(_selectedTrendPointId, out var data) || data.Count == 0)
                return;

            long min = data[0].DateTime.Ticks;
            long max = min + TimeSpan.FromSeconds(MaxTrendPoints).Ticks;

            // 范围没变就不动轴，避免每秒重建造成的闪动
            if (min == _axisXMin && max == _axisXMax)
                return;

            _axisXMin = min;
            _axisXMax = max;
            _trendXAxis.MinLimit = min;
            _trendXAxis.MaxLimit = max;

            // 重新赋值触发图表刷新（同一个轴实例，只更新边界）
            TrendXAxes = new Axis[] { _trendXAxis };
        }

        /// <summary>按当前测点的数据范围设置 Y 轴（取整到好读的刻度；范围不变时不重建轴）</summary>
        private void UpdateTrendYAxis()
        {
            if (_selectedTrendPointId == -1)
                return;

            var values = _trendDataCollections[_selectedTrendPointId]
                .Where(p => p.Value.HasValue)
                .Select(p => p.Value!.Value)
                .ToList();

            double min = 0, max = 100;

            if (values.Count > 0)
            {
                min = values.Min();
                max = values.Max();

                // 数值恒定（设备稳定）时给一个可读跨度，否则曲线会被放大成锯齿
                double span = max - min;
                if (span < 1e-9)
                    span = Math.Max(Math.Abs(max) * 0.1, 1);

                double pad = span * 0.25;
                min -= pad;
                max += pad;

                // 取整到 1/2/5×10ⁿ 的刻度，刻度值好读、范围也不会每秒微调
                double step = NiceStep((max - min) / 4);
                min = Math.Floor(min / step) * step;
                max = Math.Ceiling(max / step) * step;
            }

            if (min == _axisMin && max == _axisMax)
                return;

            _axisMin = min;
            _axisMax = max;
            TrendYAxes = new Axis[] { CreateValueAxis(min, max) };
        }

        /// <summary>把刻度跨度取整到 1/2/5×10ⁿ</summary>
        private static double NiceStep(double rough)
        {
            if (rough <= 0)
                return 1;

            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
            double normalized = rough / magnitude;
            double nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
            return nice * magnitude;
        }

        /// <summary>更新报警阈值虚线：上下限画成横贯数据窗口的虚线（超出当前 Y 轴范围时不显示）</summary>
        private void UpdateThresholds()
        {
            var data = _selectedPoint != null && _trendDataCollections.TryGetValue(_selectedPoint.DataPointId, out var collection)
                ? collection
                : null;

            DateTime start = DateTime.Now.AddSeconds(-MaxTrendPoints);
            DateTime end = DateTime.Now;

            if (data is { Count: > 0 })
            {
                start = data[0].DateTime;
                end = data[^1].DateTime;
            }

            SetThreshold(_highThresholdSeries, _highThresholdPoints, _selectedPoint?.AlarmHigh, start, end);
            SetThreshold(_lowThresholdSeries, _lowThresholdPoints, _selectedPoint?.AlarmLow, start, end);
        }

        /// <summary>把一条阈值线拉到 [start, end] 区间</summary>
        private static void SetThreshold(LineSeries<DateTimePoint> series,
            ObservableCollection<DateTimePoint> points, double? value, DateTime start, DateTime end)
        {
            series.IsVisible = value.HasValue;

            if (value is null)
                return;

            if (points.Count == 2)
            {
                points[0] = new DateTimePoint(start, value);
                points[1] = new DateTimePoint(end, value);
            }
            else
            {
                points.Clear();
                points.Add(new DateTimePoint(start, value));
                points.Add(new DateTimePoint(end, value));
            }
        }

        /// <summary>图表左上角标题：测点名 + 单位</summary>
        private void UpdateTrendTitle(DataPointDisplayModel? pointInfo)
        {
            TrendTitle = pointInfo == null
                ? string.Empty
                : string.IsNullOrWhiteSpace(pointInfo.Unit)
                    ? pointInfo.Name
                    : $"{pointInfo.Name} · {pointInfo.Unit}";
        }

        // ======== 事件处理方法 ========

        /// <summary>TreeView 选中设备时：加载设备数据并初始化趋势图</summary>
        private void OnSelectedItemChanged(object? selectedItem)
        {
            if (selectedItem is DeviceTreeNode device)
            {
                IsDeviceSelected = true;
                _selectedDeviceId = device.Id;

                // 换代：在途的旧设备响应回来后会被丢弃
                _deviceGeneration++;
                _pendingTrendInit = true;

                // 切换设备 → 清空趋势图与测点集合所有数据
                _trendDataCollections.Clear();
                _dataPoints.Clear();
                _viewPoints.Clear();
                _selectedPoint = null;
                RaisePropertyChanged(nameof(SelectedPoint));
                RaisePropertyChanged(nameof(IsCardMode));
                RaisePropertyChanged(nameof(IsTableMode));
                _selectedTrendPointId = -1;
                TrendTitle = string.Empty;
                _axisMin = double.NaN;
                _axisMax = double.NaN;
                _axisXMin = long.MinValue;
                _axisXMax = long.MinValue;
                _highThresholdSeries.IsVisible = false;
                _lowThresholdSeries.IsVisible = false;

                _ = LoadDeviceDataAsync(initializeTrend: true); // 首次加载需初始化
            }
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
            // 设备树只加载一次（后面导航回来不再重复加载）
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

        /// <summary>全部测点（服务端顺序）：测点选择器数据源，不受搜索/筛选影响</summary>
        public ObservableCollection<DataPointDisplayModel> DataPoints => _dataPoints;

        /// <summary>界面渲染的测点：已按搜索词/仅看报警筛选并报警置顶排序</summary>
        public ObservableCollection<DataPointDisplayModel> ViewPoints => _viewPoints;

        /// <summary>搜索关键字（按名称过滤）</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    RefreshViewPoints();
            }
        }

        /// <summary>仅看报警</summary>
        public bool OnlyAlarms
        {
            get => _onlyAlarms;
            set
            {
                if (SetProperty(ref _onlyAlarms, value))
                    RefreshViewPoints();
            }
        }

        /// <summary>≤12 个测点用卡片，超过则自动切表格</summary>
        public bool IsCardMode => _dataPoints.Count <= 12;

        /// <summary>卡片模式的反向属性：>12 个测点用表格</summary>
        public bool IsTableMode => !IsCardMode;

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

        /// <summary>
        /// 当前选中的测点（卡片/表格/测点下拉共用）。
        /// 切换时同步更新 _selectedTrendPointId、Series 数据源、X/Y 轴与阈值虚线。
        /// </summary>
        public DataPointDisplayModel? SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                // 筛选重排导致控件回传 null 时忽略，保持当前曲线不中断
                if (value == null || ReferenceEquals(value, _selectedPoint))
                    return;

                _selectedPoint = value;
                RaisePropertyChanged();

                _selectedTrendPointId = value.DataPointId;
                _axisMin = double.NaN;   // 换测点必须重算量程
                _axisMax = double.NaN;
                _axisXMin = long.MinValue;
                _axisXMax = long.MinValue;
                UpdateTrendSeries();     // 切换 Series 绑定的数据集合
                UpdateTrendXAxis();      // X 轴跟着新测点的数据区间
                UpdateTrendYAxis();      // 调整 Y 轴范围
                UpdateThresholds();      // 换测点的报警阈值不同
            }
        }

        public ISeries[] TrendSeries
        {
            get => _trendSeries;
            set => SetProperty(ref _trendSeries, value);
        }

        public Axis[] TrendXAxes
        {
            get => _trendXAxes;
            set => SetProperty(ref _trendXAxes, value);
        }

        public Axis[] TrendYAxes
        {
            get => _trendYAxes;
            set => SetProperty(ref _trendYAxes, value);
        }

        /// <summary>图表标题（测点名 · 单位）</summary>
        public string TrendTitle
        {
            get => _trendTitle;
            set => SetProperty(ref _trendTitle, value);
        }

        /// <summary>tooltip 样式：白底深灰字，替代默认的灰底小方框</summary>
        public SolidColorPaint TooltipBackgroundPaint { get; } =
            new(SKColors.White) { StrokeThickness = 1 };

        public SolidColorPaint TooltipTextPaint { get; } =
            new(SKColor.Parse("#333333"));
    }
}