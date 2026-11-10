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
        private int _currentPage = 1;
        private int _totalPages = 1;
        private string _pageInfo = "第 1 页";

        // === View 命令 ===
        public DelegateCommand RefreshCurrentCommand { get; }
        public DelegateCommand AcknowledgeCommand { get; }
        public DelegateCommand PreviousPageCommand { get; }
        public DelegateCommand NextPageCommand { get; }

        // === 筛选选项 ===

        public string[] AlarmStatuses { get; } = { "全部", "Unacknowledged", "Acknowledged" };

        public AlarmViewModel(IAlarmService alarmService)
        {
            _alarmService = alarmService;

            RefreshCurrentCommand = new DelegateCommand(async () => await LoadAlarmsAsync());
            AcknowledgeCommand = new DelegateCommand(async () => await AcknowledgeSelectedAlarmAsync(), CanAcknowledge);
            PreviousPageCommand = new DelegateCommand(async () => await PreviousPageAsync(), CanGoPrevious);
            NextPageCommand = new DelegateCommand(async () => await NextPageAsync(), CanGoNext);

            _ = LoadAlarmsAsync();
        }

        // === INavigationAware ===
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 每次导航进入时回到第一页
            _currentPage = 1;
            _ = LoadAlarmsAsync();
        }

        // === 按当前筛选加载一页报警 ===
        private async Task LoadAlarmsAsync()
        {
            string? status = SelectedStatus == "全部" ? null : SelectedStatus;

            var result = await _alarmService.GetHistoryAsync(_currentPage, 10, null, status);
            if (result != null)
            {
                Alarms = new ObservableCollection<AlarmDto>(result.Items);
                _totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / 10.0));
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
                await LoadAlarmsAsync();
            }
        }

        private bool CanAcknowledge() => SelectedAlarm != null && SelectedAlarm.Status == "Unacknowledged";

        // === 分页 ===
        private async Task PreviousPageAsync()
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await LoadAlarmsAsync();
            }
        }

        private async Task NextPageAsync()
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                await LoadAlarmsAsync();
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
                    _ = LoadAlarmsAsync();  // 切换状态后回到第一页重新加载
                }
            }
        }

        public string PageInfo
        {
            get => _pageInfo;
            set => SetProperty(ref _pageInfo, value);
        }
    }
}