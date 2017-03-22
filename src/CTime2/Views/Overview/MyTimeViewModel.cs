using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using CTime2.Core.Data;
using CTime2.Core.Events;
using CTime2.Core.Services.ApplicationState;
using CTime2.Core.Services.CTime;
using CTime2.Core.Services.Statistics;
using CTime2.Strings;
using CTime2.Views.About;
using CTime2.Views.YourTimes;
using ReactiveUI;
using UwCore;
using UwCore.Application.Events;
using UwCore.Common;
using UwCore.Extensions;
using UwCore.Services.ApplicationState;
using UwCore.Services.Navigation;

namespace CTime2.Views.Overview
{
    public class MyTimeViewModel : UwCoreScreen, IHandleWithTask<ApplicationResumed>, IHandleWithTask<UserStamped>
    {
        private readonly IApplicationStateService _applicationStateService;
        private readonly ICTimeService _cTimeService;
        private readonly INavigationService _navigationService;
        private readonly IStatisticsService _statisticsService;

        private readonly Timer _timer;
        
        private Time _time;

        private TimeSpan _currentTime;
        private TimeSpan? _overTime;
        private TimeSpan? _lunchBreakTime;
        private DateTime? _preferedLunchBreakEnd;

        public TimeSpan CurrentTime
        {
            get { return this._currentTime; }
            set { this.RaiseAndSetIfChanged(ref this._currentTime, value); }
        }

        public TimeSpan? OverTime
        {
            get { return this._overTime; }
            set { this.RaiseAndSetIfChanged(ref this._overTime, value); }
        }

        public TimeSpan? LunchBreakTime
        {
            get { return this._lunchBreakTime; }
            set { this.RaiseAndSetIfChanged(ref this._lunchBreakTime, value); }
        }
        
        public DateTime? PreferedLunchBreakEnd
        {
            get { return this._preferedLunchBreakEnd; }
            set { this.RaiseAndSetIfChanged(ref this._preferedLunchBreakEnd, value); }
        }

        public UwCoreCommand<Unit> RefreshTimer { get; }

        public UwCoreCommand<Unit> GoToMyTimes { get; }

        public MyTimeViewModel(IApplicationStateService applicationStateService, ICTimeService cTimeService, IEventAggregator eventAggregator, INavigationService navigationService, IStatisticsService statisticsService)
        {
            Guard.NotNull(applicationStateService, nameof(applicationStateService));
            Guard.NotNull(cTimeService, nameof(cTimeService));
            Guard.NotNull(eventAggregator, nameof(eventAggregator));
            Guard.NotNull(navigationService, nameof(navigationService));
            Guard.NotNull(statisticsService, nameof(statisticsService));

            this._applicationStateService = applicationStateService;
            this._cTimeService = cTimeService;
            this._navigationService = navigationService;
            this._statisticsService = statisticsService;

            this._timer = new Timer(this.Tick, null, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));

            this.RefreshTimer = UwCoreCommand.Create(this.RefreshTimerImpl)
                .ShowLoadingOverlay(CTime2Resources.Get("Loading.CurrentTime"))
                .HandleExceptions()
                .TrackEvent("RefreshTimer");

            this.GoToMyTimes = UwCoreCommand.Create(this.GoToMyTimesImpl)
                .HandleExceptions()
                .TrackEvent("TimerGoToMyTimes");

            eventAggregator.SubscribeScreen(this);
        }

        protected override async void OnActivate()
        {
            base.OnActivate();

            await this.RefreshTimer.ExecuteAsync();
        }

        private async Task RefreshTimerImpl()
        {
            this._time = await this._cTimeService.GetCurrentTime(this._applicationStateService.GetCurrentUser().Id);

            this.UpdateTimes();

            var statistic = this._statisticsService.CalculateCurrentTime(this._time);
            if (statistic.IsStillRunning)
            {
                this._timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
            }
            else
            {
                this._timer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            }
        }

        private Task GoToMyTimesImpl()
        {
            this._navigationService.For<YourTimesViewModel>()
                .WithParam(f => f.Parameter.StartDate, DateTimeOffset.Now.WithoutTime())
                .WithParam(f => f.Parameter.EndDate, DateTimeOffset.Now.WithoutTime())
                .Navigate();

            return Task.CompletedTask;
        }

        private void Tick(object state)
        {
            Execute.OnUIThread(this.UpdateTimes);
        }

        private void UpdateTimes()
        {
            var statistic = this._statisticsService.CalculateCurrentTime(this._time);

            this.CurrentTime = statistic.WorkTime;
            this.LunchBreakTime = statistic.CurrentBreak?.BreakTime;
            this.OverTime = statistic.OverTime;
            this.PreferedLunchBreakEnd = statistic.CurrentBreak?.PreferredBreakTimeEnd;
        }

        async Task IHandleWithTask<ApplicationResumed>.Handle(ApplicationResumed message)
        {
            await this.RefreshTimer.ExecuteAsync();
        }

        async Task IHandleWithTask<UserStamped>.Handle(UserStamped message)
        {
            await this.RefreshTimer.ExecuteAsync();
        }
    }
}