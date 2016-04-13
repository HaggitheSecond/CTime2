﻿using Windows.UI.Xaml.Controls;
using Caliburn.Micro;
using CTime2.Common;
using CTime2.Core.Services.SessionState;
using CTime2.Services.Navigation;
using CTime2.Strings;
using CTime2.Views.AttendanceList;
using CTime2.Views.Overview;
using CTime2.Views.Settings;
using CTime2.Views.Shell;
using CTime2.Views.StampTime;
using CTime2.Views.Statistics;
using CTime2.Views.YourTimes;

namespace CTime2.States
{
    public class LoggedInApplicationState : ApplicationState
    {
        private readonly ICTimeNavigationService _navigationService;
        private readonly ISessionStateService _sessionStateService;

        private readonly HamburgerItem _overviewHamburgerItem;
        private readonly HamburgerItem _stampTimeHamburgerItem;
        private readonly HamburgerItem _myTimesHamburgerItem;
        private readonly HamburgerItem _attendanceListHamburgerItem;
        private readonly HamburgerItem _logoutHamburgerItem;
        private readonly HamburgerItem _statisticsItem;

        public LoggedInApplicationState(ICTimeNavigationService navigationService, ISessionStateService sessionStateService)
        {
            this._navigationService = navigationService;
            this._sessionStateService = sessionStateService;

            this._overviewHamburgerItem = new NavigatingHamburgerItem(CTime2Resources.Get("Navigation.Overview"), Symbol.Globe, typeof(OverviewViewModel));
            this._stampTimeHamburgerItem = new NavigatingHamburgerItem(CTime2Resources.Get("Navigation.Stamp"), Symbol.Clock, typeof(StampTimeViewModel));
            this._myTimesHamburgerItem = new NavigatingHamburgerItem(CTime2Resources.Get("Navigation.MyTimes"), Symbol.Calendar, typeof(YourTimesViewModel));
            this._attendanceListHamburgerItem = new NavigatingHamburgerItem(CTime2Resources.Get("Navigation.AttendanceList"), SymbolEx.AttendanceList, typeof(AttendanceListViewModel));
            this._logoutHamburgerItem = new ClickableHamburgerItem(CTime2Resources.Get("Navigation.Logout"), SymbolEx.Logout, this.Logout);
            this._statisticsItem = new NavigatingHamburgerItem(CTime2Resources.Get("Navigation.Statistics"), SymbolEx.Statistics, typeof(StatisticsViewModel));
        }

        public override void Enter()
        {
            this.Application.Actions.Add(this._overviewHamburgerItem);
            this.Application.Actions.Add(this._stampTimeHamburgerItem);
            this.Application.Actions.Add(this._myTimesHamburgerItem);
            this.Application.Actions.Add(this._attendanceListHamburgerItem);
            this.Application.SecondaryActions.Add(this._logoutHamburgerItem);
            this.Application.Actions.Add(this._statisticsItem);

            this._navigationService.Navigate(typeof(OverviewViewModel));
        }

        public override void Leave()
        {
            this.Application.Actions.Remove(this._overviewHamburgerItem);
            this.Application.Actions.Remove(this._stampTimeHamburgerItem);
            this.Application.Actions.Remove(this._myTimesHamburgerItem);
            this.Application.Actions.Remove(this._attendanceListHamburgerItem);
            this.Application.SecondaryActions.Remove(this._logoutHamburgerItem);
            this.Application.Actions.Remove(this._statisticsItem);
        }

        private async void Logout()
        {
            this._sessionStateService.CurrentUser = null;
            await this._sessionStateService.SaveStateAsync();

            this.Application.CurrentState = IoC.Get<LoggedOutApplicationState>();
        }
    }
}