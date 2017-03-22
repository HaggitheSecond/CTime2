using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using CTime2.Core.Services.ApplicationState;
using CTime2.Core.Services.Biometrics;
using CTime2.Strings;
using Microsoft.HockeyApp;
using ReactiveUI;
using UwCore;
using UwCore.Application;
using UwCore.Common;
using UwCore.Services.ApplicationState;
using CTime2.Extensions;
using Microsoft.Toolkit.Uwp.Notifications;

namespace CTime2.Views.Settings
{
    public class SettingsViewModel : UwCoreScreen
    {
        private readonly IBiometricsService _biometricsService;
        private readonly IApplicationStateService _applicationStateService;
        private readonly IShell _shell;
        private readonly IHockeyClient _hockeyClient;

        private ReactiveList<TimeSpan> _workTimes;
        private TimeSpan _selectedWorkTime;
        private ReactiveList<TimeSpan> _breakTimes;
        private TimeSpan _selectedBreakTime;
        private ReactiveList<TimeSpan> _breakTimeAreas;
        private TimeSpan _breakTimeBegin;
        private TimeSpan _breakTimeEnd;
        private ReactiveList<DayOfWeek> _workDays;
        private ElementTheme _theme;
        private bool _includeContactInfoInErrorReports;
        private string _companyId;

        public ReactiveList<TimeSpan> WorkTimes
        {
            get { return this._workTimes; }
            set { this.RaiseAndSetIfChanged(ref this._workTimes, value); }
        }

        public TimeSpan SelectedWorkTime
        {
            get { return this._selectedWorkTime; }
            set { this.RaiseAndSetIfChanged(ref this._selectedWorkTime, value); }
        }

        public ReactiveList<TimeSpan> BreakTimes
        {
            get { return this._breakTimes; }
            set { this.RaiseAndSetIfChanged(ref this._breakTimes, value); }
        }

        public TimeSpan SelectedBreakTime
        {
            get { return this._selectedBreakTime; }
            set { this.RaiseAndSetIfChanged(ref this._selectedBreakTime, value); }
        }

        public ReactiveList<TimeSpan> BreakTimeAreas
        {
            get { return this._breakTimeAreas; }
            set { this.RaiseAndSetIfChanged(ref this._breakTimeAreas, value); }
        }

        public TimeSpan BreakTimeBegin
        {
            get { return this._breakTimeBegin; }
            set { this.RaiseAndSetIfChanged(ref this._breakTimeBegin, value); }
        }

        public TimeSpan BreakTimeEnd
        {
            get { return this._breakTimeEnd; }
            set { this.RaiseAndSetIfChanged(ref this._breakTimeEnd, value); }
        }

        public ReactiveList<DayOfWeek> WorkDays
        {
            get { return this._workDays; }
            set { this.RaiseAndSetIfChanged(ref this._workDays, value); }
        }

        public ElementTheme Theme
        {
            get { return this._theme; }
            set { this.RaiseAndSetIfChanged(ref this._theme, value); }
        }

        public bool IncludeContactInfoInErrorReports
        {
            get { return this._includeContactInfoInErrorReports; }
            set { this.RaiseAndSetIfChanged(ref this._includeContactInfoInErrorReports, value); }
        }

        public string CompanyId
        {
            get { return this._companyId; }
            set { this.RaiseAndSetIfChanged(ref this._companyId, value); }
        }
        
        public UwCoreCommand<Unit> RememberLogin { get; }

        public SettingsViewModel(IBiometricsService biometricsService, IApplicationStateService applicationStateService, IShell shell, IHockeyClient hockeyClient)
        {
            Guard.NotNull(biometricsService, nameof(biometricsService));
            Guard.NotNull(applicationStateService, nameof(applicationStateService));
            Guard.NotNull(shell, nameof(shell));
            Guard.NotNull(hockeyClient, nameof(hockeyClient));

            this._biometricsService = biometricsService;
            this._applicationStateService = applicationStateService;
            this._shell = shell;
            this._hockeyClient = hockeyClient;

            var hasUser = new ReplaySubject<bool>(1);
            hasUser.OnNext(this._applicationStateService.GetCurrentUser() != null);
            var deviceAvailable = this._biometricsService.BiometricAuthDeviceIsAvailableAsync().ToObservable();
            var canRememberLogin = hasUser.CombineLatest(deviceAvailable, (hasUsr, deviceAvail) => hasUsr && deviceAvail);
            this.RememberLogin = UwCoreCommand.Create(canRememberLogin, this.RememberLoginImpl)
                .ShowLoadingOverlay(CTime2Resources.Get("Loading.RememberedLogin"))
                .HandleExceptions()
                .TrackEvent("SetupRememberLogin");
            
            this.Theme = this._applicationStateService.GetApplicationTheme();
            this.SelectedWorkTime = this._applicationStateService.GetWorkDayHours();
            this.SelectedBreakTime = this._applicationStateService.GetWorkDayBreak();
            this.BreakTimeBegin = this._applicationStateService.GetBreakTimeBegin();
            this.BreakTimeEnd = this._applicationStateService.GetBreakTimeEnd();
            this.WorkDays = new ReactiveList<DayOfWeek>(this._applicationStateService.GetWorkDays());
            this.CompanyId = this._applicationStateService.GetCompanyId();
            this.IncludeContactInfoInErrorReports = this._applicationStateService.GetIncludeContactInfoInErrorReports();

            this.WhenAnyValue(f => f.Theme)
                .Subscribe(theme =>
                {
                    this._shell.Theme = theme;
                    this._applicationStateService.SetApplicationTheme(theme);
                });

            this.WhenAnyValue(f => f.SelectedWorkTime)
                .Subscribe(workTime =>
                {
                    this._applicationStateService.SetWorkDayHours(workTime);
                });

            this.WhenAnyValue(f => f.SelectedBreakTime)
                .Subscribe(breakTime =>
                {
                    this._applicationStateService.SetWorkDayBreak(breakTime);
                });

            this.WhenAnyValue(f => f.BreakTimeBegin)
                .Subscribe(breakTimeBegin =>
                {
                    this._applicationStateService.SetBreakTimeBegin(breakTimeBegin);
                });

            this.WhenAnyValue(f => f.BreakTimeEnd)
                .Subscribe(breakTimeEnd =>
                {
                    this._applicationStateService.SetBreakTimeEnd(breakTimeEnd);
                });

            this.WhenAnyValue(f => f.CompanyId)
                .Subscribe(companyId =>
                {
                    this._applicationStateService.SetCompanyId(companyId);
                });

            this.WhenAnyValue(f => f.IncludeContactInfoInErrorReports)
                .Subscribe(include =>
                {
                    this._applicationStateService.SetIncludeContactInfoInErrorReports(include);

                    var currentUser = this._applicationStateService.GetCurrentUser();
                    this._hockeyClient.UpdateContactInfo(this._applicationStateService.GetIncludeContactInfoInErrorReports() ? currentUser : null);
                });

            this.WorkDays.Changed
                .Subscribe(_ =>
                {
                    this._applicationStateService.SetWorkDays(this.WorkDays.ToArray());
                });

            this.DisplayName = CTime2Resources.Get("Navigation.Settings");

            this.WorkTimes = new ReactiveList<TimeSpan>(Enumerable
                .Repeat((object)null, 4 * 24)
                .Select((_, i) => TimeSpan.FromHours(0.25 * i)));

            this.BreakTimes = new ReactiveList<TimeSpan>(Enumerable
                .Repeat((object)null, 4 * 24)
                .Select((_, i) => TimeSpan.FromHours(0.25 * i)));

            this.BreakTimeAreas = new ReactiveList<TimeSpan>(Enumerable
                .Repeat((object)null, 4 * 24)
                .Select((_, i) => TimeSpan.FromHours(0.25 * i)));
        }

        private async Task RememberLoginImpl()
        {
            var user = this._applicationStateService.GetCurrentUser();
            await this._biometricsService.RememberUserForBiometricAuthAsync(user);
        }
    }
}