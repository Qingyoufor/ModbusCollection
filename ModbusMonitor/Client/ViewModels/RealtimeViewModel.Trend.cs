using Client.Models;
using Client.Models.Dtos;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace Client.ViewModels
{
    public partial class RealtimeViewModel
    {
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

        /// <summary>X/Y 轴统一的标签字号</summary>
        private const double AxisTextSize = 18;

        /// <summary>X/Y 轴统一的刻度文字画笔</summary>
        private static readonly SolidColorPaint AxisLabelsPaint = new(SKColor.Parse("#8C8C8C"));

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
                TextSize = AxisTextSize,
                LabelsPaint = AxisLabelsPaint,
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#F0F0F0")) { StrokeThickness = 1 }
            };

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

        /// <summary>为所有测点追加新数值（滑动窗口：超出 MaxTrendPoints 时移除最早的数据点）</summary>
        private void UpdateTrendCollections(RealtimeDeviceDataDto device)
        {
            var now = DateTime.Now;

            foreach (var p in device.DataPoints)
            {
                var collection = GetOrCreateTrendCollection(p.DataPointId);
                collection.Add(new DateTimePoint(now, p.Value));

                // 滑动窗口：保持最多 60 个点（1 秒 1 点 = 60 秒历史）
                if (collection.Count > MaxTrendPoints)
                {
                    collection.RemoveAt(0);
                }
            }
        }

        /// <summary>取测点的趋势集合；服务端运行中新增的测点还没建集合时就地补一个</summary>
        private ObservableCollection<DateTimePoint> GetOrCreateTrendCollection(int dataPointId)
        {
            if (!_trendDataCollections.TryGetValue(dataPointId, out var collection))
            {
                collection = new ObservableCollection<DateTimePoint>();
                _trendDataCollections[dataPointId] = collection;
            }

            return collection;
        }

        // ======== 趋势图方法 ========

        /// <summary>切换测点时重建 Series，绑定到对应测点的 ObservableCollection（不清空历史数据）</summary>
        private void UpdateTrendSeries()
        {
            if (_selectedPoint == null)
                return;

            var values = GetOrCreateTrendCollection(_selectedPoint.DataPointId);

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
                    Fill = new LinearGradientPaint(
                        TrendColor.WithAlpha(70),
                        TrendColor.WithAlpha(0),
                        new SKPoint(0.5f, 0),
                        new SKPoint(0.5f, 1)),
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
        private void UpdateTrendTitle(DataPointDisplayModel point)
        {
            TrendTitle = string.IsNullOrWhiteSpace(point.Unit)
                ? point.Name
                : $"{point.Name} · {point.Unit}";
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
