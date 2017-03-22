using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Caliburn.Micro;
using CTime2.Core.Data;
using CTime2.Core.Events;
using CTime2.Core.Services.ApplicationState;
using CTime2.Core.Services.CTime;
using CTime2.Core.Services.Statistics;
using CTime2.Core.Strings;
using Microsoft.Toolkit.Uwp.Notifications;
using UwCore.Application.Events;
using UwCore.Common;
using UwCore.Events;
using UwCore.Extensions;
using UwCore.Services.ApplicationState;

namespace CTime2.Core.Services.Tile
{
    [AutoSubscribeEvents]
    public class TileService : ITileService, IHandleWithTask<ApplicationResumed>, IHandleWithTask<UserStamped>, IHandleWithTask<ShellModeEntered>
    {
        private readonly IApplicationStateService _applicationStateService;
        private readonly IApplicationStateService _myApplicationStateService;
        private readonly ICTimeService _cTimeService;
        private readonly IStatisticsService _statisticsService;

        public DateTime StartDateForStatistics
        {
            get { return this._myApplicationStateService.Get<DateTime?>("StartDateForStatistics", UwCore.Services.ApplicationState.ApplicationState.Local) ?? DateTimeOffset.Now.StartOfMonth().LocalDateTime; }
            set { this._myApplicationStateService.Set("StartDateForStatistics", value, UwCore.Services.ApplicationState.ApplicationState.Local); }
        }

        public DateTime EndDateForStatistics
        {
            get { return this._myApplicationStateService.Get<DateTime?>("EndDateForStatistics", UwCore.Services.ApplicationState.ApplicationState.Local) ?? DateTimeOffset.Now.WithoutTime().LocalDateTime; }
            set { this._myApplicationStateService.Set("EndDateForStatistics", value, UwCore.Services.ApplicationState.ApplicationState.Local); }
        }

        public bool IncludeTodayForStatistics
        {
            get { return this._myApplicationStateService.Get<bool?>("IncludeTodayForStatistics", UwCore.Services.ApplicationState.ApplicationState.Local) ?? false; }
            set { this._myApplicationStateService.Set("IncludeTodayForStatistics", value, UwCore.Services.ApplicationState.ApplicationState.Local); }
        }

        public TileService(IApplicationStateService applicationStateService, ICTimeService cTimeService, IStatisticsService statisticsService)
        {
            Guard.NotNull(applicationStateService, nameof(applicationStateService));
            Guard.NotNull(cTimeService, nameof(cTimeService));
            Guard.NotNull(statisticsService, nameof(statisticsService));
            
            this._applicationStateService = applicationStateService;
            this._myApplicationStateService = applicationStateService.GetStateServiceFor(typeof(TileService));
            this._cTimeService = cTimeService;
            this._statisticsService = statisticsService;
        }
        
        public async Task UpdateLiveTileAsync()
        {
            var updateManager = TileUpdateManager.CreateTileUpdaterForApplication();

            var currentUser = this._applicationStateService.GetCurrentUser();
            if (currentUser == null)
            {
                updateManager.Clear();
                return;
            }

            var timesTodayTask = this._cTimeService.GetTimes(currentUser.Id, DateTime.Today, DateTime.Today);
            var currentTimeTask = this._cTimeService.GetCurrentTime(currentUser.Id);
            var statisticsTask = this._cTimeService.GetTimes(currentUser.Id, this.StartDateForStatistics, this.EndDateForStatistics);

            await Task.WhenAll(timesTodayTask, currentTimeTask, statisticsTask);

            var timesToday = TimesByDay.Create(await timesTodayTask).FirstOrDefault();
            var currentTime = await currentTimeTask;
            var statistics = TimesByDay.Create(await statisticsTask).ToList();

            if (this._statisticsService.ShouldIncludeToday(statistics) == false)
            {
                statistics = statistics.Where(f => f.Day != DateTime.Today).ToList();
            }

            TileContent content = new TileContent
            {
                Visual = new TileVisual
                {
                    Branding = TileBranding.NameAndLogo,
                    TileMedium = this.GetTileMedium(currentTime, timesToday),
                    TileWide = this.GetTileWide(currentTime, timesToday),
                    TileLarge = this.GetTileLarge(currentTime, timesToday, statistics),
                },
            };

            var notification = new TileNotification(content.GetXml());
            notification.ExpirationTime = DateTimeOffset.Now.AddMinutes(30);

            updateManager.Update(notification);
        }

        private TileBinding GetTileLarge(Time currentTime, TimesByDay today, List<TimesByDay> statistics)
        {
            var wideGroup = new AdaptiveGroup();
            foreach (var part in this.GetMediumAndWideParts(currentTime, today).Take(2))
            {
                wideGroup.Children.Add(part);
            }

            var statisticsGroup = new AdaptiveGroup
            {
                Children = {this.CreateStatisticsGroup(currentTime, today, statistics) }
            };

            return new TileBinding
            {
                Branding = TileBranding.None, //We need all vertical space we can get
                Content = new TileBindingContentAdaptive
                {
                    Children =
                    {
                        wideGroup,
                        new AdaptiveText(), //A little bit of free space
                        statisticsGroup,
                    }
                }
            };
        }

        private TileBinding GetTileWide(Time currentTime, TimesByDay today)
        {
            var group = new AdaptiveGroup();

            foreach (var part in this.GetMediumAndWideParts(currentTime, today).Take(2))
            {
                group.Children.Add(part);
            }
            
            return new TileBinding
            {
                Content = new TileBindingContentAdaptive
                {
                    Children =
                    {
                        group
                    }
                }
            };
        }

        private TileBinding GetTileMedium(Time currentTime, TimesByDay today)
        {
            var content = new TileBindingContentAdaptive();

            foreach (var part in this.GetMediumAndWideParts(currentTime, today))
            {
                content.Children.Add(new AdaptiveGroup { Children = { part }});
            }

            return new TileBinding { Content = content };
        }

        #region Tile Parts
        private IEnumerable<AdaptiveSubgroup> GetMediumAndWideParts(Time currentTime, TimesByDay today)
        {
            var parts = new Func<Time, TimesByDay, AdaptiveSubgroup>[]
            {
                this.CreateRunningTimeGroup,
                this.CreateTimesTodayGroup,
            };

            foreach (var part in parts)
            {
                var group = part(currentTime, today);

                if (group != null)
                    yield return group;
            }
        }

        private AdaptiveSubgroup CreateRunningTimeGroup(Time currentTime, TimesByDay today)
        {
            var statistics = this._statisticsService.CalculateCurrentTime(currentTime);

            if (statistics.WorkTime == TimeSpan.Zero)
                return null;

            var group = new AdaptiveSubgroup
            {
                Children =
                {
                    new AdaptiveText
                    {
                        Text = CTime2CoreResources.Get("LiveTile.YourTime"),
                        HintStyle = AdaptiveTextStyle.Base
                    },
                    new AdaptiveText
                    {
                        Text = $"> {statistics.WorkTime.Hours} h {this.RoundDownTo5(statistics.WorkTime.Minutes)} min",
                        HintStyle = AdaptiveTextStyle.CaptionSubtle
                    }
                }
            };

            if (statistics.OverTime != null)
            {
                group.Children.Add(new AdaptiveText
                {
                    Text = CTime2CoreResources.Get("LiveTile.OverTime")
                });
                group.Children.Add(new AdaptiveText
                {
                    Text = $"> {statistics.OverTime.Value.Hours} h {this.RoundDownTo5(statistics.OverTime.Value.Minutes)} min",
                    HintStyle = AdaptiveTextStyle.CaptionSubtle
                });
            }

            if (statistics.CurrentBreak != null)
            {
                group.Children.Add(new AdaptiveText
                {
                    Text = CTime2CoreResources.Get("LiveTile.Break")
                });
                group.Children.Add(new AdaptiveText
                {
                    Text = $"> {statistics.CurrentBreak.BreakTime.Hours} h {this.RoundDownTo5(statistics.CurrentBreak.BreakTime.Minutes)} min",
                    HintStyle = AdaptiveTextStyle.CaptionSubtle
                });
                group.Children.Add(new AdaptiveText
                {
                    Text = CTime2CoreResources.GetFormatted("LiveTile.BreakUntil", statistics.CurrentBreak.PreferredBreakTimeEnd.ToString("t")),
                    HintStyle = AdaptiveTextStyle.CaptionSubtle
                });
            }

            return group;
        }

        private AdaptiveSubgroup CreateTimesTodayGroup(Time currentTime, TimesByDay today)
        {
            var group = new AdaptiveSubgroup
            {
                Children =
                {
                    new AdaptiveText
                    {
                        Text = CTime2CoreResources.Get("LiveTile.TimesToday"),
                        HintStyle = AdaptiveTextStyle.Base
                    }
                }
            };

            foreach (var time in today.Times)
            {
                group.Children.Add(new AdaptiveText
                {
                    Text = $"{time.ClockInTime?.ToString("t") ?? "?"} - {time.ClockOutTime?.ToString("t") ?? "?"}",
                    HintStyle = AdaptiveTextStyle.CaptionSubtle
                });
            }

            return group;
        }

        private AdaptiveSubgroup CreateStatisticsGroup(Time currentTime, TimesByDay today, List<TimesByDay> statistics)
        {
            var overTime = this._statisticsService.CalculateOverTime(statistics, onlyWorkDays: false);
            var todaysWorkEnd = this._statisticsService.CalculateTodaysWorkEnd(today, statistics, onlyWorkDays: false);

            var result = new AdaptiveSubgroup
            {
                Children =
                {
                    new AdaptiveText
                    {
                        Text = CTime2CoreResources.Get("LiveTile.Statistics"),
                        HintStyle = AdaptiveTextStyle.Base
                    },
                    new AdaptiveText
                    {
                        Text = CTime2CoreResources.Get("LiveTile.OverTime")
                    },
                    new AdaptiveText
                    {
                        Text = $"{overTime.TotalMinutes} min",
                        HintStyle = AdaptiveTextStyle.CaptionSubtle
                    }
                }
            };

            if (todaysWorkEnd != null)
            {
                result.Children.Add(new AdaptiveText
                {
                    Text = CTime2CoreResources.Get("LiveTile.TodaysWorkEnd")
                });
                result.Children.Add(new AdaptiveText
                {
                    Text = CTime2CoreResources.GetFormatted("LiveTile.TodaysWorkEndWithOvertimeFormat", todaysWorkEnd.WithOvertime.ToString("t")),
                    HintStyle = AdaptiveTextStyle.CaptionSubtle
                });
                result.Children.Add(new AdaptiveText
                {
                    Text = CTime2CoreResources.GetFormatted("LiveTile.TodaysWorkEndWithoutOvertimeFormat", todaysWorkEnd.WithoutOvertime.ToString("t")),
                    HintStyle = AdaptiveTextStyle.CaptionSubtle
                });
            }

            return result;
        }

        private int RoundDownTo5(int value)
        {
            return (value / 5) * 5;
        }
        #endregion

        #region Events
        Task IHandleWithTask<ApplicationResumed>.Handle(ApplicationResumed message)
        {
            return this.UpdateLiveTileAsync();
        }
        Task IHandleWithTask<UserStamped>.Handle(UserStamped message)
        {
            return this.UpdateLiveTileAsync();
        }
        Task IHandleWithTask<ShellModeEntered>.Handle(ShellModeEntered message)
        {
            return this.UpdateLiveTileAsync();
        }
        #endregion
    }
}