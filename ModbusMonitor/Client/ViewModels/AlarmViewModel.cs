// Client/ViewModels/AlarmViewModel.cs
using Client.Models.Dtos;
using Client.Service;
using System.Collections.ObjectModel;

namespace Client.ViewModels
{
    public class AlarmViewModel : BindableBase, INavigationAware
    {
        private readonly IAlarmService _alarmService;

        // === View 字段 ===
        private ObservableCollection<AlarmDto> _alarms = new();
        private AlarmDto? _selectedAlarm;

        private string _selectedStatus = "Unacknowledged";
        private bool _isHistoryMode;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private string _pageInfo = "第 1 页";

        // === View 命令 ===
        public DelegateCommand RefreshCurrentCommand { get; }
        public DelegateCommand LoadHistoryCommand { get; }
        public DelegateCommand AcknowledgeCommand { get; }
        public DelegateCommand PreviousPageCommand { get; }
        public DelegateCommand NextPageCommand { get; }

        // === 筛选选项 ===

        public string[] AlarmStatuses { get; } = { "全部", "Unacknowledged", "Acknowledged" };

        public AlarmViewModel(IAlarmService alarmService)
        {
            _alarmService = alarmService;

            RefreshCurrentCommand = new DelegateCommand(async () => await LoadCurrentAlarmsAsync());
            LoadHistoryCommand = new DelegateCommand(async () => await LoadHistoryAlarmsAsync());
            AcknowledgeCommand = new DelegateCommand(async () => await AcknowledgeSelectedAlarmAsync(), CanAcknowledge);
            PreviousPageCommand = new DelegateCommand(async () => await PreviousPageAsync(), CanGoPrevious);
            NextPageCommand = new DelegateCommand(async () => await NextPageAsync(), CanGoNext);

            // 初始加载当前报警
            _ = LoadCurrentAlarmsAsync();
        }

        // === INavigationAware ===
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 每次导航进入时刷新当前报警
            _ = LoadCurrentAlarmsAsync();
        }

        // === 加载当前报警 ===
        private async Task LoadCurrentAlarmsAsync()
        {
            IsHistoryMode = false;
            SelectedStatus = AlarmStatuses[1];
            var alarms = await _alarmService.GetCurrentAlarmsAsync();
            if (alarms != null)
            {
                Alarms = new ObservableCollection<AlarmDto>(alarms);
            }
        }

        // === 加载历史报警 ===
        private async Task LoadHistoryAlarmsAsync()
        {
            IsHistoryMode = true;
            _currentPage = 1;
            await LoadHistoryPageAsync();
        }

        private async Task LoadHistoryPageAsync()
        {

            string? status = SelectedStatus == "全部" ? null : SelectedStatus;

            var result = await _alarmService.GetHistoryAsync(_currentPage, 10, null, status);
            if (result != null)
            {
                Alarms = new ObservableCollection<AlarmDto>(result.Items);
                _totalPages = (int)Math.Ceiling(result.TotalCount / 10.0);
                PageInfo = $"第 {_currentPage}/{_totalPages} 页（共 {result.TotalCount} 条）";

                // 更新按钮状态
                PreviousPageCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
            }
        }

        // === 确认报警 ===
        private async Task AcknowledgeSelectedAlarmAsync()
        {
            if (SelectedAlarm == null)
                return;

            var success = await _alarmService.AcknowledgeAsync(SelectedAlarm.Id);
            if (success)
            {
                // 刷新列表
                if (IsHistoryMode)
                    await LoadHistoryPageAsync();
                else
                    await LoadCurrentAlarmsAsync();
            }
        }

        private bool CanAcknowledge() => SelectedAlarm != null && SelectedAlarm.Status == "Unacknowledged";

        // === 分页 ===
        private async Task PreviousPageAsync()
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await LoadHistoryPageAsync();
            }
        }

        private async Task NextPageAsync()
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                await LoadHistoryPageAsync();
            }
        }

        private bool CanGoPrevious() => _currentPage > 1;
        private bool CanGoNext() => _currentPage < _totalPages;

        // === 属性 ===
        public ObservableCollection<AlarmDto> Alarms
        {
            get => _alarms;
            set => SetProperty(ref _alarms, value);
        }

        public AlarmDto? SelectedAlarm
        {
            get => _selectedAlarm;
            set
            {
                SetProperty(ref _selectedAlarm, value);
                AcknowledgeCommand.RaiseCanExecuteChanged();
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                {
                    _currentPage = 1;
                    _ = LoadHistoryPageAsync();  // 切换等级后重新加载
                }
            }
        }

        public bool IsHistoryMode
        {
            get => _isHistoryMode;
            set => SetProperty(ref _isHistoryMode, value);
        }

        public string PageInfo
        {
            get => _pageInfo;
            set => SetProperty(ref _pageInfo, value);
        }
    }
}