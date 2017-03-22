using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.VoiceCommands;
using Caliburn.Micro;
using CTime2.Core.Data;
using CTime2.Core.Services.CTime;
using CTime2.Core.Services.CTime.RequestCache;
using CTime2.Core.Services.GeoLocation;
using CTime2.Core.Services.Statistics;
using CTime2.Core.Services.Tile;
using UwCore.Services.ApplicationState;

namespace CTime2.LiveTileService
{
    public sealed class CTime2LiveTileService : IBackgroundTask
    {
        #region Logger
        private static readonly ILog Logger = LogManager.GetLog(typeof(CTime2LiveTileService));
        #endregion

        #region Fields
        private BackgroundTaskDeferral _deferral;
        #endregion

        #region Methods
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            try
            {
                this._deferral = taskInstance.GetDeferral();

                taskInstance.Canceled += (s, e) => this.Close();

                var applicationStateService = new ApplicationStateService();
                await applicationStateService.RestoreStateAsync();

                var geoLocationService = new GeoLocationService();
                var eventAggregator = new EventAggregator();
                var ctimeRequestCache = new NullCTimeRequestCache();
                var ctimeService = new CTimeService(ctimeRequestCache, eventAggregator, applicationStateService, geoLocationService);
                var statisticsService = new StatisticsService(applicationStateService);
                var liveTileService = new TileService(applicationStateService, ctimeService, statisticsService);

                await liveTileService.UpdateLiveTileAsync();
            }
            catch (Exception exception)
            {
                Logger.Warn("Exception occured in the live-tile service.");
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
    }
}
