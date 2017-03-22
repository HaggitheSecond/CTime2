using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.StartScreen;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Caliburn.Micro;
using CTime2.ApplicationModes;
using CTime2.Core.Common;
using CTime2.Core.Services.ApplicationState;
using CTime2.Core.Services.Biometrics;
using CTime2.Core.Services.Contacts;
using CTime2.Core.Services.CTime;
using CTime2.Core.Services.CTime.RequestCache;
using CTime2.Core.Services.Email;
using CTime2.Core.Services.EmployeeGroups;
using CTime2.Core.Services.GeoLocation;
using CTime2.Core.Services.Licenses;
using CTime2.Core.Services.Phone;
using CTime2.Core.Services.Sharing;
using CTime2.Core.Services.Statistics;
using CTime2.Core.Services.Tile;
using CTime2.LiveTileService;
using CTime2.Strings;
using CTime2.Views.About;
using CTime2.Views.AttendanceList;
using CTime2.Views.GeoLocationInfo;
using CTime2.Views.HeaderDetails;
using CTime2.Views.Login;
using CTime2.Views.Overview;
using CTime2.Views.Overview.CheckedIn;
using CTime2.Views.Overview.CheckedOut;
using CTime2.Views.Settings;
using CTime2.Views.Statistics;
using CTime2.Views.Statistics.Details;
using CTime2.Views.Statistics.Details.BreakTime;
using CTime2.Views.Statistics.Details.EnterAndLeaveTime;
using CTime2.Views.Statistics.Details.OverTime;
using CTime2.Views.Statistics.Details.WorkTime;
using CTime2.Views.Terminal;
using CTime2.Views.UpdateNotes;
using CTime2.Views.YourTimes;
using Microsoft.Toolkit.Uwp;
using UwCore.Application;
using UwCore.Extensions;
using UwCore.Hamburger;
using UwCore.Logging;
using UwCore.Services.ApplicationState;

namespace CTime2
{
    sealed partial class App
    {
        public App()
        {
            this.InitializeComponent();
        }

        public override void Configure()
        {
            base.Configure();

            this.ConfigureVoiceCommands();
            this.ConfigureWindowMinSize();
            this.ConfigureJumpList();
            this.ConfigureLiveTileService();
        }

        public override void CustomizeShell(IShell shell)
        {
            base.CustomizeShell(shell);
            
            shell.Theme = IoC.Get<IApplicationStateService>().GetApplicationTheme();

            shell.HeaderDetailsViewModel = IoC.Get<HeaderDetailsViewModel>();

            shell.SecondaryActions.Add(new NavigatingHamburgerItem(CTime2Resources.Get("Navigation.About"), Symbol.ContactInfo, typeof(AboutViewModel)));
            shell.SecondaryActions.Add(new NavigatingHamburgerItem(CTime2Resources.Get("Navigation.Settings"), Symbol.Setting, typeof(SettingsViewModel)));
        }

        public override ShellMode GetCurrentMode()
        {
            return IoC.Get<IApplicationStateService>().GetCurrentUser() != null
                ? (ShellMode)IoC.Get<LoggedInApplicationMode>()
                : IoC.Get<LoggedOutApplicationMode>();
        }

        public override async void AppStartupFinished()
        {
            await IoC.Get<ITileService>().UpdateLiveTileAsync();
        }

        public override string GetErrorTitle()
        {
            return CTime2Resources.Get("ExceptionHandler.ErrorTitle");
        }

        public override string GetErrorMessage()
        {
            return CTime2Resources.Get("ExceptionHandler.ErrorMessage");
        }

        public override Type GetCommonExceptionType()
        {
            return typeof(CTimeException);
        }

        public override IEnumerable<Type> GetViewModelTypes()
        {
            yield return typeof(LoginViewModel);
            yield return typeof(OverviewViewModel);
            yield return typeof(YourTimesViewModel);
            yield return typeof(AboutViewModel);
            yield return typeof(AttendanceListViewModel);
            yield return typeof(StatisticsViewModel);
            yield return typeof(CheckedInViewModel);
            yield return typeof(CheckedOutViewModel);
            yield return typeof(SettingsViewModel);
            yield return typeof(HeaderDetailsViewModel);
            yield return typeof(DetailedStatisticViewModel);
            yield return typeof(UpdateNotesViewModel);
            yield return typeof(GeoLocationInfoViewModel);
            yield return typeof(MyTimeViewModel);
            yield return typeof(AttendingUserDetailsViewModel);
            yield return typeof(TerminalViewModel);
            yield return typeof(BreakTimeViewModel);
            yield return typeof(EnterAndLeaveTimeViewModel);
            yield return typeof(OverTimeViewModel);
            yield return typeof(WorkTimeViewModel);
        }

        public override IEnumerable<Type> GetShellModeTypes()
        {
            yield return typeof(LoggedOutApplicationMode);
            yield return typeof(LoggedInApplicationMode);
            yield return typeof(TerminalApplicationMode);
        }

        public override IEnumerable<Type> GetServiceTypes()
        {
            yield return typeof(ICTimeService);
            yield return typeof(CTimeService);

            yield return typeof(ILicensesService);
            yield return typeof(LicensesService);
            
            yield return typeof(IBiometricsService);
            yield return typeof(BiometricsService);

            yield return typeof(IGeoLocationService);
            yield return typeof(GeoLocationService);

            yield return typeof(IContactsService);
            yield return typeof(ContactsService);

            yield return typeof(IEmployeeGroupService);
            yield return typeof(EmployeeGroupService);

            yield return typeof(IPhoneService);
            yield return typeof(PhoneService);

            yield return typeof(IEmailService);
            yield return typeof(EmailService);

            yield return typeof(ISharingService);
            yield return typeof(SharingService);

            yield return typeof(IStatisticsService);
            yield return typeof(StatisticsService);

            yield return typeof(ITileService);
            yield return typeof(TileService);

            yield return typeof(ICTimeRequestCache);
            yield return typeof(CTimeRequestCache);
        }

        public override bool IsHockeyAppEnabled()
        {
            return base.IsHockeyAppEnabled() &&
                   Package.Current.Id.Version.ToVersion() != new Version("9999.9999.9999.0");
        }

        public override string GetHockeyAppId()
        {
            return "16f525b1f6c04b6b987253bd8801dc20";
        }

        public override Type GetUpdateNotesViewModelType()
        {
            return typeof(UpdateNotesViewModel);
        }

        private async void ConfigureVoiceCommands()
        {
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///CTime2.VoiceCommandService/CTime2VoiceCommands.xml"));
                await VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(file);
            }
            catch (Exception exception)
            {
                var log = LogManager.GetLog(typeof(App));

                log.Warn($"For some reason the voice-command registration failed. {exception.GetFullMessage()}");
                log.Error(exception);
            }
        }

        private void ConfigureWindowMinSize()
        {
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(380, 500));
        }
        
        private async void ConfigureJumpList()
        {
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 2, 0) && JumpList.IsSupported())
            {
                JumpList jumpList = await JumpList.LoadCurrentAsync();
                jumpList.Items.Clear();

                var checkInArguments = new CTimeStartupArguments { Action = CTimeStartupAction.Checkin };
                var checkInItem = JumpListItem.CreateWithArguments(StartupArguments.AsString(checkInArguments), CTime2Resources.Get("StampHelper.CheckIn"));
                jumpList.Items.Add(checkInItem);

                var checkOutArguments = new CTimeStartupArguments { Action = CTimeStartupAction.Checkout };
                var checkOutItem = JumpListItem.CreateWithArguments(StartupArguments.AsString(checkOutArguments), CTime2Resources.Get("StampHelper.CheckOut"));
                jumpList.Items.Add(checkOutItem);

                await jumpList.SaveAsync();
            }
        }

        private async void ConfigureLiveTileService()
        {
            var access = await BackgroundExecutionManager.RequestAccessAsync();
            
            var validStatus = new[]
            {
                BackgroundAccessStatus.AlwaysAllowed, 
                BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity, 
                BackgroundAccessStatus.AllowedSubjectToSystemPolicy, 
                BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity, 
            };

            if (validStatus.Contains(access))
            {
                BackgroundTaskHelper.Register(
                    typeof(CTime2LiveTileService),
                    new TimeTrigger(15, false),
                    forceRegister: true,
                    enforceConditions: true,
                    conditions: new SystemCondition(SystemConditionType.InternetAvailable));
            }
        }
    }
}
