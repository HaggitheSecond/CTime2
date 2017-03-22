using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.VoiceCommands;
using Caliburn.Micro;
using CTime2.Core.Data;
using CTime2.Core.Services.CTime;
using CTime2.Core.Services.CTime.RequestCache;
using CTime2.Core.Services.GeoLocation;
using CTime2.VoiceCommandService.Strings;
using UwCore.Logging;
using UwCore.Services.ApplicationState;

namespace CTime2.VoiceCommandService
{
    public sealed class CTime2VoiceCommandService : IBackgroundTask
    {
        #region Logger
        private static readonly ILog Logger = LogManager.GetLog(typeof(CTime2VoiceCommandService));
        #endregion

        #region Fields
        private BackgroundTaskDeferral _deferral;
        private VoiceCommandServiceConnection _connection;
        #endregion

        #region Methods
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            this._deferral = taskInstance.GetDeferral();

            try
            {
                taskInstance.Canceled += (s, e) => this.Close();

                var triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;

                if (triggerDetails != null &&
                    triggerDetails.Name == "CTime2VoiceCommandService")
                {
                    this._connection = VoiceCommandServiceConnection.FromAppServiceTriggerDetails(triggerDetails);
                    this._connection.VoiceCommandCompleted += (s, e) => this.Close();

                    var voiceCommand = await this._connection.GetVoiceCommandAsync();

                    Logger.Info($"Executing voice command '{voiceCommand.CommandName}'.");

                    var applicationStateService = new ApplicationStateService();
                    await applicationStateService.RestoreStateAsync();
                    var cTimeService = new CTimeService(new NullCTimeRequestCache(), new EventAggregator(), applicationStateService, new GeoLocationService());

                    var stampHelper = new CTimeStampHelper(applicationStateService, cTimeService);
                    var stampHelperCallback = new CTimeStampHelperCallback(
                        this.OnNotLoggedIn, 
                        this.SupportsQuestions, 
                        this.OnDidNothing,
                        this.OnAlreadyCheckedInWannaCheckOut,
                        this.OnAlreadyCheckedIn,
                        this.OnAlreadyCheckedOutWannaCheckIn,
                        this.OnAlreadyCheckedOut,
                        this.OnSuccess);
                    
                    switch (voiceCommand.CommandName)
                    {
                        case "checkIn":
                            await stampHelper.Stamp(stampHelperCallback, TimeState.Entered);
                            break;
                        case "checkOut":
                            await stampHelper.Stamp(stampHelperCallback, TimeState.Left);
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Warn("Exception occured in the voice command service.");
                Logger.Error(exception);
            }
            finally
            {
                this.Close();
            }
        }
        #endregion

        #region Private Methods
        private void Close()
        {
            this._deferral.Complete();
        }
        #endregion

        #region Callbacks
        private async Task OnNotLoggedIn()
        {
            var message = new VoiceCommandUserMessage
            {
                DisplayMessage = CTime2VoiceCommandServiceResources.Get("NotLoggedInDisplayMessage"),
                SpokenMessage = CTime2VoiceCommandServiceResources.Get("NotLoggedInSpokenMessage"),
            };

            var response = VoiceCommandResponse.CreateResponse(message);
            await this._connection.ReportFailureAsync(response);
        }

        private bool SupportsQuestions()
        {
            return true;
        }

        private async Task OnDidNothing()
        {
            var message = new VoiceCommandUserMessage
            {
                DisplayMessage = CTime2VoiceCommandServiceResources.Get("DidNothing"),
                SpokenMessage = CTime2VoiceCommandServiceResources.Get("DidNothing"),
            };

            var response = VoiceCommandResponse.CreateResponse(message);
            await this._connection.ReportFailureAsync(response);
        }

        private async Task<bool> OnAlreadyCheckedInWannaCheckOut()
        {
            var promptMessage = new VoiceCommandUserMessage
            {
                DisplayMessage = CTime2VoiceCommandServiceResources.Get("AlreadyCheckedInDisplayMessage"),
                SpokenMessage = CTime2VoiceCommandServiceResources.Get("AlreadyCheckedInSpokenMessage"),
            };
            var rePromptMessage = new VoiceCommandUserMessage
            {
                DisplayMessage = CTime2VoiceCommandServiceResources.Get("AlreadyCheckedInDisplayMessageRepeat"),
                SpokenMessage = CTime2VoiceCommandServiceResources.Get("AlreadyCheckedInSpokenMessageRepeat"),
            };

            var response = VoiceCommandResponse.CreateResponseForPrompt(promptMessage, rePromptMessage);
            var result = await this._connection.RequestConfirmationAsync(response);

            return result?.Confirmed == true;
        }

        private Task OnAlreadyCheckedIn()
        {
            throw new NotImplementedException();
        }

        private async Task<bool> OnAlreadyCheckedOutWannaCheckIn()
        {
            var promptMessage = new VoiceCommandUserMessage
            {
                DisplayMessage = CTime2VoiceCommandServiceResources.Get("AlreadyCheckedOutDisplayMessage"),
                SpokenMessage = CTime2VoiceCommandServiceResources.Get("AlreadyCheckedOutSpokenMessage"),
            };
            var rePromptMessage = new VoiceCommandUserMessage
            {
                DisplayMessage = CTime2VoiceCommandServiceResources.Get("AlreadyCheckedOutDisplayMessageRepeat"),
                SpokenMessage = CTime2VoiceCommandServiceResources.Get("AlreadyCheckedOutSpokenMessageRepeat"),
            };

            var response = VoiceCommandResponse.CreateResponseForPrompt(promptMessage, rePromptMessage);
            var result = await this._connection.RequestConfirmationAsync(response);

            return result?.Confirmed == true;
        }

        private Task OnAlreadyCheckedOut()
        {
            throw new NotImplementedException();
        }

        private async Task OnSuccess(TimeState timeState)
        {
            var message = new VoiceCommandUserMessage
            {
                DisplayMessage = timeState.IsEntered()
                    ? CTime2VoiceCommandServiceResources.Get("SuccessfullyCheckedIn")
                    : CTime2VoiceCommandServiceResources.Get("SuccessfullyCheckedOut"),
                SpokenMessage = timeState.IsEntered()
                    ? CTime2VoiceCommandServiceResources.Get("SuccessfullyCheckedIn")
                    : CTime2VoiceCommandServiceResources.Get("SuccessfullyCheckedOut"),
            };

            var response = VoiceCommandResponse.CreateResponse(message);
            await this._connection.ReportSuccessAsync(response);
        }
        #endregion
    }
}
