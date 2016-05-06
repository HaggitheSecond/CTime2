﻿using CTime2.Core.Data;
using CTime2.Core.Services.CTime;
using UwCore.Services.ApplicationState;
using UwCore.Services.ExceptionHandler;
using UwCore.Services.Loading;

namespace CTime2.Views.StampTime.CheckedOut
{
    public class CheckedOutViewModel : StampTimeStateViewModelBase
    {
        public CheckedOutViewModel(ICTimeService cTimeService, IApplicationStateService sessionStateService, ILoadingService loadingService, IExceptionHandler exceptionHandler)
            : base(cTimeService, sessionStateService, loadingService, exceptionHandler)
        {
        }

        public async void CheckIn()
        {
            await this.Stamp(TimeState.Entered, "Loading.CheckIn");
        }

        public async void CheckInHomeOffice()
        {
            await this.Stamp(TimeState.Entered | TimeState.HomeOffice, "Loading.CheckIn");
        }

        public async void CheckInTrip()
        {
            await this.Stamp(TimeState.Entered | TimeState.Trip, "Loading.CheckIn");
        }
    }
}