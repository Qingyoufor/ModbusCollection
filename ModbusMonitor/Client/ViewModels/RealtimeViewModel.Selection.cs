using Client.Models;
using Client.Models.Dtos;
using System.Collections.ObjectModel;

namespace Client.ViewModels
{
    public partial class RealtimeViewModel
    {
        // 主集合：保持服务端返回顺序，每秒就地更新同一个对象
        private readonly ObservableCollection<DataPointDisplayModel> _dataPoints = new();
        // 界面渲染集合：经搜索/仅看报警筛选并报警置顶排序；内容无变化时不重建
        private readonly ObservableCollection<DataPointDisplayModel> _viewPoints = new();
        private string _searchText = string.Empty;
        private bool _onlyAlarms;
        private DataPointDisplayModel? _selectedPoint;

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

            for (int i = _viewPoints.Count - 1; i >= 0; i--)
            {
                if (!keep.Contains(_viewPoints[i]))
                {
                    _viewPoints.RemoveAt(i);
                }
            }

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

                _ = LoadDeviceDataAsync(initializeTrend: true);
            }
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

                // 该测点还没有采样点（服务端运行中新增的测点）：立刻拉一次，不等下一次轮询
                if (!_trendDataCollections.TryGetValue(value.DataPointId, out var trend) || trend.Count == 0)
                    _ = LoadDeviceDataAsync();
            }
        }
    }
}
