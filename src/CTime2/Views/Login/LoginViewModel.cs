﻿using System;
using System.Reactive;
using System.Threading.Tasks;
using Caliburn.Micro;
using Caliburn.Micro.ReactiveUI;
using CTime2.Core.Services.ApplicationState;
using CTime2.Core.Services.CTime;
using CTime2.States;
using CTime2.Strings;
using ReactiveUI;
using UwCore.Application;
using UwCore.Services.ApplicationState;
using UwCore.Services.Dialog;
using UwCore.Extensions;

namespace CTime2.Views.Login
{
    public class LoginViewModel : ReactiveScreen
    {
        private readonly ICTimeService _cTimeService;
        private readonly IApplicationStateService _applicationStateService;
        private readonly IApplication _application;
        private readonly IDialogService _dialogService;

        private string _emailAddress;
        private string _password;
        
        public string EmailAddress
        {
            get { return this._emailAddress; }
            set { this.RaiseAndSetIfChanged(ref this._emailAddress, value); }
        }

        public string Password
        {
            get { return this._password; }
            set { this.RaiseAndSetIfChanged(ref this._password, value); }
        }

        public ReactiveCommand<Unit> Login { get; }

        public LoginViewModel(ICTimeService cTimeService, IApplicationStateService applicationStateService, IApplication application, IDialogService dialogService)
        {
            this._cTimeService = cTimeService;
            this._applicationStateService = applicationStateService;
            this._application = application;
            this._dialogService = dialogService;

            var canLogin = this.WhenAnyValue(f => f.EmailAddress, f => f.Password, (email, password) =>
                string.IsNullOrWhiteSpace(email) == false && string.IsNullOrWhiteSpace(password) == false);
            this.Login = ReactiveCommand.CreateAsyncTask(canLogin, _ => this.LoginImpl());
            this.Login.AttachLoadingService(CTime2Resources.Get("Loading.LoggingIn"));
            this.Login.AttachExceptionHandler();

            this.DisplayName = CTime2Resources.Get("Navigation.Login");
        }
        
        private async Task LoginImpl()
        {
            var user = await this._cTimeService.Login(this.EmailAddress, this.Password);

            if (user == null)
            {
                await this._dialogService.ShowAsync(CTime2Resources.Get("Login.LoginFailed"));
                return;
            }
                    
            this._applicationStateService.SetCurrentUser(user);;

            await this._applicationStateService.SaveStateAsync();

            this._application.CurrentMode = IoC.Get<LoggedInApplicationState>();
        }
    }
}