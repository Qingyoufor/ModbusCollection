using Client.Infrastructure;
using Client.Models;
using Client.Models.Dtos;
using Client.Service;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace Client.ViewModels
{
    public class HistoryViewModel : BindableBase, INavigationAware
    {
        private readonly IRealtimeService _realtimeService;
        private readonly IHistoryService _historyService;

        // 设备树与测点
        private ObservableCollection<DeviceGroupNode> _deviceGroupNodes = new();
        private readonly ObservableCollection<DataPointDisplayModel> _dataPoints = new();
        private readonly ObservableCollection<DataPointDisplayModel> _pointsView = new();
        private string _pointSearchText = string.Empty;
        private DeviceTreeNode? _selectedDevice;
        private DataPointDisplayModel? _selectedPoint;

        // 查询条件
        private DateTime _startTime;
        private DateTime _endTime;

        private bool _isLoading;
        private bool _hasQueried;

        // 竞态控制：切设备、连续点选测点时只认最新一次请求的结果
        private int _deviceGeneration;
        private int _queryGeneration;

        // 空状态提示
        private string _chartHint = "请在左侧选择设备";
        private bool _isChartEmpty = true;

        // 趋势图
        private ISeries[] _trendSeries = Array.Empty<ISeries>();
        private Axis[] _trendXAxes;
        private Axis[] _trendYAxes;

        // 统计
        private HistoryStatisticsDto _statistics = new();

        public DelegateCommand QueryCommand { get; }
        public DelegateCommand ExportCommand { get; }
        public DelegateCommand<object?> SelectedItemChangedCommand { get; }
        public DelegateCommand LastHourCommand { get; }
        public DelegateCommand TodayCommand { get; }
        public DelegateCommand Last7DaysCommand { get; }

        public HistoryViewModel(IRealtimeService realtimeService, IHistoryService historyService)
        {
            _realtimeService = realtimeService;
            _historyService = historyService;

            // 默认查询范围为最近 1 小时（本地时间；发送时转成 UTC 与服务端存储一致）
            _endTime = DateTime.Now;
            _startTime = _endTime.AddHours(-1);

            SelectedItemChangedCommand = new DelegateCommand<object?>(OnSelectedItemChanged);
            QueryCommand = new DelegateCommand(async () => await QueryAsync(), CanQuery);
            ExportCommand = new DelegateCommand(async () => await ExportAsync(), CanExport);

            LastHourCommand = new DelegateCommand(() => ApplyQuickRange(DateTime.Now.AddHours(-1), DateTime.Now));
            TodayCommand = new DelegateCommand(() => ApplyQuickRange(DateTime.Today, DateTime.Now));
            Last7DaysCommand = new DelegateCommand(() => ApplyQuickRange(DateTime.Now.AddDays(-7), DateTime.Now));

            // 默认坐标轴配置
            TrendXAxes = new Axis[]
            {
                new Axis { Name = "时间", NameTextSize = 12 }
            };
            TrendYAxes = new Axis[]
            {
                new Axis { Name = "数值", NameTextSize = 12 }
            };
        }

        /// <summary>加载全部设备，按串口分组构建设备树</summary>
        private async Task LoadTreeAsync()
        {
            var allData = await _realtimeService.GetAllDataAsync();
            if (allData == null)
                return;

            DeviceGroupNodes = new ObservableCollection<DeviceGroupNode>(
                allData.Devices
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

        /// <summary>树选中变化：分组节点只给提示，设备节点加载测点并自动查询第一个测点</summary>
        private void OnSelectedItemChanged(object? selectedItem)
        {
            if (selectedItem is DeviceGroupNode)
            {
                ResetForNoDevice("请选择串口下的设备");
                return;
            }

            if (selectedItem is DeviceTreeNode device)
                _ = LoadPointsAsync(device);
        }

        /// <summary>选中设备：加载测点列表，自动选中第一个测点并立即查询</summary>
        private async Task LoadPointsAsync(DeviceTreeNode device)
        {
            int generation = ++_deviceGeneration;
            InvalidateQueries();

            _selectedDevice = device;
            ClearPoints();
            _hasQueried = false;
            Statistics = new HistoryStatisticsDto();
            SetChartState(empty: true, "请选择测点");

            var deviceData = await _realtimeService.GetDeviceDataAsync(device.Id);

            // 请求期间又换了设备：丢弃过期响应
            if (deviceData == null || generation != _deviceGeneration)
                return;

            foreach (var p in deviceData.DataPoints)
            {
                _dataPoints.Add(new DataPointDisplayModel
                {
                    DataPointId = p.DataPointId,
                    Name = p.Name,
                    Unit = p.Unit,
                    AlarmHigh = p.AlarmHigh,
                    AlarmLow = p.AlarmLow
                });
            }

            ApplyPointFilter();
            RaiseCommandStates();

            if (_dataPoints.Count == 0)
            {
                SetChartState(empty: true, "该设备没有测点");
                return;
            }

            // 走 SelectedPoint 的 setter：自动触发一次查询
            SelectedPoint = _pointsView.Count > 0 ? _pointsView[0] : _dataPoints[0];
        }

        /// <summary>按测点名称筛选左侧列表（不改变已选中的曲线）</summary>
        private void ApplyPointFilter()
        {
            string keyword = _pointSearchText.Trim();

            var desired = _dataPoints
                .Where(p => keyword.Length == 0 || p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (desired.SequenceEqual(_pointsView))
                return;

            _pointsView.Clear();
            foreach (var point in desired)
            {
                _pointsView.Add(point);
            }

            // 筛选后控件会自行清空选中，这里把选中项重新推回；setter 忽略 null，需绕过
            if (_selectedPoint != null && _pointsView.Contains(_selectedPoint))
            {
                var selected = _selectedPoint;
                _selectedPoint = null;
                RaisePropertyChanged(nameof(SelectedPoint));
                _selectedPoint = selected;
                RaisePropertyChanged(nameof(SelectedPoint));
            }
        }

        /// <summary>按当前起止时间查询历史数据，并刷新曲线与统计</summary>
        private async Task QueryAsync()
        {
            var device = _selectedDevice;
            var point = _selectedPoint;
            if (device == null || point == null)
                return;

            int generation = ++_queryGeneration;
            IsLoading = true;
            RaiseCommandStates();

            // 服务端存储为 UTC，查询边界转成 UTC，避免时区偏移
            var startUtc = _startTime.ToUniversalTime();
            var endUtc = _endTime.ToUniversalTime();

            try
            {
                var dataTask = _historyService.GetHistoryAsync(device.Id, point.DataPointId, startUtc, endUtc);
                var statsTask = _historyService.GetStatisticsAsync(device.Id, point.DataPointId, startUtc, endUtc);

                await Task.WhenAll(dataTask, statsTask);

                // 期间又点了别的测点：丢弃这次的结果
                if (generation != _queryGeneration)
                    return;

                var data = await dataTask;
                var stats = await statsTask;

                _hasQueried = true;

                if (data is { Count: > 0 })
                {
                    // 服务端返回 UTC 时间，转成本地时间再绘图
                    var values = new ObservableCollection<DateTimePoint>(
                        data.Select(d => new DateTimePoint(d.RecordedAt.ToLocalFromUtc(), d.Value)));

                    TrendSeries = new ISeries[]
                    {
                        new LineSeries<DateTimePoint>
                        {
                            Values = values,
                            Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                            Fill = null,
                            GeometryFill = null,
                            GeometryStroke = null
                        }
                    };

                    // 刻度密度按查询跨度自适应：查 7 天时不会再挤一万个刻度
                    TrendXAxes = new Axis[]
                    {
                        new DateTimeAxis(GetTickStep(), t => t.ToString(GetLabelFormat()))
                        {
                            Name = "时间",
                            NameTextSize = 12
                        }
                    };

                    TrendYAxes = new Axis[]
                    {
                        new Axis
                        {
                            Name = _selectedPoint?.Unit ?? "数值",
                            NameTextSize = 12
                        }
                    };

                    SetChartState(empty: false, string.Empty);
                }
                else
                {
                    TrendSeries = Array.Empty<ISeries>();
                    SetChartState(empty: true, "该时间段没有数据");
                }

                Statistics = stats ?? new HistoryStatisticsDto();
            }
            catch (Exception)
            {
                // 自动查询是用户点一下就会触发的，网络异常不能让 async void 命令把进程带崩
                if (generation == _queryGeneration)
                {
                    TrendSeries = Array.Empty<ISeries>();
                    SetChartState(empty: true, "查询失败，请检查服务端连接");
                }
            }
            finally
            {
                // 只有最新一次查询才负责收尾，避免旧请求把加载态提前关掉
                if (generation == _queryGeneration)
                {
                    IsLoading = false;
                    RaiseCommandStates();
                }
            }
        }

        /// <summary>刻度密度：≤2 小时用 5 分钟、≤1 天用 1 小时、更长用 1 天</summary>
        private TimeSpan GetTickStep()
        {
            var span = _endTime - _startTime;

            if (span <= TimeSpan.FromHours(2))
                return TimeSpan.FromMinutes(5);

            return span <= TimeSpan.FromDays(1)
                ? TimeSpan.FromHours(1)
                : TimeSpan.FromDays(1);
        }

        /// <summary>刻度文案：一天以内只显示时分，跨天显示月日</summary>
        private string GetLabelFormat() =>
            _endTime - _startTime <= TimeSpan.FromDays(1) ? "HH:mm" : "MM-dd";

        /// <summary>快捷时间范围：设置起止时间后立即查询</summary>
        private void ApplyQuickRange(DateTime start, DateTime end)
        {
            StartTime = start;
            EndTime = end;
            _ = QueryAsync();
        }

        private bool CanQuery() => _selectedDevice != null && _selectedPoint != null && !IsLoading;

        /// <summary>调用导出 API，弹出保存对话框写入 CSV</summary>
        private async Task ExportAsync()
        {
            if (_selectedDevice == null || _selectedPoint == null)
                return;

            var bytes = await _historyService.ExportAsync(
                _selectedDevice.Id, _selectedPoint.DataPointId,
                _startTime.ToUniversalTime(), _endTime.ToUniversalTime());
            if (bytes == null)
                return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"history_{_selectedDevice.Name}_{_selectedPoint.Name}.csv",
                Filter = "CSV 文件 (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                await System.IO.File.WriteAllBytesAsync(dialog.FileName, bytes);
            }
        }

        private bool CanExport() => _selectedDevice != null && _selectedPoint != null && !IsLoading;

        // ====== 状态重置 ======

        /// <summary>未选中设备（或选中分组节点）：清空测点、曲线与统计</summary>
        private void ResetForNoDevice(string hint)
        {
            _deviceGeneration++;
            InvalidateQueries();

            _selectedDevice = null;
            ClearPoints();
            _hasQueried = false;
            Statistics = new HistoryStatisticsDto();
            TrendSeries = Array.Empty<ISeries>();
            SetChartState(empty: true, hint);
            RaiseCommandStates();
        }

        /// <summary>清空测点集合与选中项</summary>
        private void ClearPoints()
        {
            _dataPoints.Clear();
            _pointsView.Clear();

            if (_selectedPoint != null)
            {
                _selectedPoint = null;
                RaisePropertyChanged(nameof(SelectedPoint));
            }
        }

        /// <summary>作废在途查询：响应回来时对不上代际号就会被丢弃</summary>
        private void InvalidateQueries()
        {
            _queryGeneration++;
            IsLoading = false;
        }

        private void SetChartState(bool empty, string hint)
        {
            ChartHint = hint;
            IsChartEmpty = empty;
        }

        private void RaiseCommandStates()
        {
            QueryCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
        }

        // ====== INavigationAware ======
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (DeviceGroupNodes.Count == 0)
                _ = LoadTreeAsync();
        }

        // ====== 属性 ======

        public ObservableCollection<DeviceGroupNode> DeviceGroupNodes
        {
            get => _deviceGroupNodes;
            set => SetProperty(ref _deviceGroupNodes, value);
        }

        /// <summary>左侧测点列表（按搜索词过滤后）</summary>
        public ObservableCollection<DataPointDisplayModel> PointsView => _pointsView;

        /// <summary>测点搜索关键字</summary>
        public string PointSearchText
        {
            get => _pointSearchText;
            set
            {
                if (SetProperty(ref _pointSearchText, value))
                    ApplyPointFilter();
            }
        }

        public DataPointDisplayModel? SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                // 筛选导致控件回传 null 时忽略，保持当前曲线不中断
                if (value == null || ReferenceEquals(value, _selectedPoint))
                    return;

                _selectedPoint = value;
                RaisePropertyChanged();
                RaiseCommandStates();

                // 点选测点立即查询，不等用户再按「查询」
                _ = QueryAsync();
            }
        }

        public DateTime StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        public DateTime EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>图表空状态文案</summary>
        public string ChartHint
        {
            get => _chartHint;
            private set => SetProperty(ref _chartHint, value);
        }

        /// <summary>图表是否为空（显示空状态文案）</summary>
        public bool IsChartEmpty
        {
            get => _isChartEmpty;
            private set => SetProperty(ref _isChartEmpty, value);
        }

        public HistoryStatisticsDto Statistics
        {
            get => _statistics;
            set
            {
                if (SetProperty(ref _statistics, value))
                {
                    RaisePropertyChanged(nameof(HasStatistics));
                    RaisePropertyChanged(nameof(MaxText));
                    RaisePropertyChanged(nameof(MinText));
                    RaisePropertyChanged(nameof(AverageText));
                    RaisePropertyChanged(nameof(CountText));
                }
            }
        }

        /// <summary>查到了数据才显示统计值</summary>
        public bool HasStatistics => _hasQueried && _statistics.Count > 0;

        public string MaxText => HasStatistics ? $"最大：{_statistics.Max:F2}" : "最大：—";
        public string MinText => HasStatistics ? $"最小：{_statistics.Min:F2}" : "最小：—";
        public string AverageText => HasStatistics ? $"平均：{_statistics.Average:F2}" : "平均：—";
        public string CountText => HasStatistics ? $"点数：{_statistics.Count}" : "点数：—";

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
    }
}
