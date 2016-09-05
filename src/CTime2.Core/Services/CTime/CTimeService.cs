﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Certificates;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Caliburn.Micro;
using CTime2.Core.Common;
using CTime2.Core.Data;
using CTime2.Core.Events;
using CTime2.Core.Strings;
using Newtonsoft.Json.Linq;
using UwCore.Common;
using UwCore.Logging;

namespace CTime2.Core.Services.CTime
{
    public class CTimeService : ICTimeService
    {
        private static readonly Logger Logger = LoggerFactory.GetLogger<CTimeService>();

        private readonly IEventAggregator _eventAggregator;

        public CTimeService(IEventAggregator eventAggregator)
        {
            Guard.NotNull(eventAggregator, nameof(eventAggregator));

            this._eventAggregator = eventAggregator;

            this.AddCTimeCertificate();
        }

        public async Task<User> Login(string emailAddress, string password)
        {
            try
            {
                var responseJson = await this.SendRequestAsync("V2/LoginV2.php", new Dictionary<string, string>
                {
                    {"Password", password},
                    {"LoginName", emailAddress}
                });

                var user = responseJson?
                    .Value<JArray>("Result")
                    .OfType<JObject>()
                    .FirstOrDefault();

                if (user == null)
                    return null;

                return new User
                {
                    Id = user.Value<string>("EmployeeGUID"),
                    CompanyId = user.Value<string>("CompanyGUID"),
                    Email = user.Value<string>("LoginName"),
                    FirstName = user.Value<string>("EmployeeFirstName"),
                    Name = user.Value<string>("EmployeeName"),
                    ImageAsPng = Convert.FromBase64String(user.Value<string>("EmployeePhoto") ?? string.Empty),
                };
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Exception in method {nameof(this.Login)}. Email address: {emailAddress}");
                throw new CTimeException(CTime2CoreResources.Get("CTimeService.ErrorWhileLogin") , exception);
            }
        }

        public async Task<IList<Time>> GetTimes(string employeeGuid, DateTime start, DateTime end)
        {
            try
            {
                var responseJson = await this.SendRequestAsync("V2/GetTimerListV2.php", new Dictionary<string, string>
                {
                    {"EmployeeGUID", employeeGuid},
                    {"DateTill", end.ToString("yyyy-MM-dd")},
                    {"DateFrom", start.ToString("yyyy-MM-dd")},
                    {"Summary", 1.ToString()}
                });

                if (responseJson == null)
                    return new List<Time>();
                
                return responseJson
                    .Value<JArray>("Result")
                    .OfType<JObject>()
                    .Select(f => new Time
                    {
                        Day = f.Value<DateTime>("DayDate"),
                        Hours = TimeSpan.Parse(f.Value<string>("TimeHour_IST_HR")),
                        State = f["TimeTrackTypePure"].ToObject<int?>() != 0 
                            ? (TimeState?)f["TimeTrackTypePure"].ToObject<int?>()
                            : null,
                        ClockInTime = f["TimeTrackIn"].ToObject<DateTime?>(),
                        ClockOutTime = f["TimeTrackOut"].ToObject<DateTime?>(),
                    })
                    .Select(f =>
                    {
                        if (f.ClockInTime != null && f.ClockOutTime != null)
                        {
                            f.State = (f.State ?? 0) | TimeState.Left;
                        }
                        else if (f.ClockInTime != null)
                        {
                            f.State = (f.State ?? 0) | TimeState.Entered;
                        }

                        return f;
                    })
                    .Where(f => f.Day <= DateTime.Today || f.ClockInTime != null || f.ClockOutTime != null)
                    .ToList();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Exception in method {nameof(this.GetTimes)}. Employee: {employeeGuid}, Start: {start}, End: {end}");
                throw new CTimeException(CTime2CoreResources.Get("CTimeService.ErrorWhileLoadingTimes"), exception);
            }
        }

        public async Task<bool> SaveTimer(string employeeGuid, DateTime time, string companyId, TimeState state)
        {
            try
            {
                var responseJson = await this.SendRequestAsync("V2/SaveTimerV2.php", new Dictionary<string, string>
                {
                    {"TimerKind", ((int) state).ToString()},
                    {"TimerText", string.Empty},
                    {"TimerTime", time.ToString("yyyy-MM-dd HH:mm:ss")},
                    {"EmployeeGUID", employeeGuid},
                    {"GUID", companyId}
                });

                if (responseJson?.Value<int>("State") == 0)
                {
                    this._eventAggregator.PublishOnCurrentThread(new UserStamped());
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Exception in method {nameof(this.SaveTimer)}. Employee: {employeeGuid}, Time: {time}, Company Id: {companyId}, State: {(int)state}");
                throw new CTimeException(CTime2CoreResources.Get("CTimeService.ErrorWhileStamp"), exception);
            }
        }

        public async Task<Time> GetCurrentTime(string employeeGuid)
        {
            try
            {
                IList<Time> timesForToday = await this.GetTimes(employeeGuid, DateTime.Today, DateTime.Today.AddDays(1));
            
                var itemsToIgnore = timesForToday
                    .Where(f =>
                        (f.ClockInTime != null && f.ClockOutTime != null) ||
                        (f.ClockInTime == null && f.ClockOutTime == null))
                    .ToList();

                Time latestFinishedTimeToday = itemsToIgnore
                    .Where(f => f.ClockInTime != null && f.ClockOutTime != null)
                    .OrderByDescending(f => f.ClockOutTime)
                    .FirstOrDefault();

                return timesForToday.FirstOrDefault(f => itemsToIgnore.Contains(f) == false) ?? latestFinishedTimeToday;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Exception in method {nameof(this.GetCurrentTime)}. Employee: {employeeGuid}");
                throw new CTimeException(CTime2CoreResources.Get("CTimeService.ErrorWhileLoadingCurrentTime"), exception);
            }
        }

        public async Task<IList<AttendingUser>> GetAttendingUsers(string companyId)
        {
            try
            {
                var responseJson = await this.SendRequestAsync("V2/GetPresenceListV2.php", new Dictionary<string, string>
                {
                    {"GUID", companyId}
                });

                if (responseJson == null)
                    return new List<AttendingUser>();

                return responseJson
                    .Value<JArray>("Result")
                    .OfType<JObject>()
                    .Select(f => new AttendingUser
                    {
                        Name = f.Value<string>("EmployeeName"),
                        FirstName = f.Value<string>("EmployeeFirstName"),
                        IsAttending = f.Value<int>("PresenceStatus") == 1,
                        ImageAsPng = Convert.FromBase64String(f.Value<string>("EmployeePhoto") ?? string.Empty),
                    })
                    .ToList();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Exception in method {nameof(this.GetAttendingUsers)}. Company Id: {companyId}");
                throw new CTimeException(CTime2CoreResources.Get("CTimeService.ErrorWhileLoadingAttendanceList"), exception);
            }
        }

        private async Task<JObject> SendRequestAsync(string function, Dictionary<string, string> data)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, this.BuildUri(function))
            {
                Content = new HttpFormUrlEncodedContent(data)
            };

            var response = await this.GetClient().SendRequestAsync(request);

            if (response.StatusCode != HttpStatusCode.Ok)
                return null;

            var responseContentAsString = await response.Content.ReadAsStringAsync();
            var responseJson = JObject.Parse(responseContentAsString);

            var responseState = responseJson.Value<int>("State");

            if (responseState != 0)
                return null;

            return responseJson;
        }

        private Uri BuildUri(string path)
        {
            return new Uri($"https://app.c-time.net/php/{path}");
        }

        private HttpClient GetClient()
        {
            var filter = new HttpBaseProtocolFilter();
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.InvalidName);

            return new HttpClient(filter);
        }

        private void AddCTimeCertificate()
        {
            using (var certificateStream = this.GetType().GetTypeInfo().Assembly.GetManifestResourceStream("CTime2.Core.Services.CTime.Certificate.cer"))
            using (var memoryStream = new MemoryStream())
            {
                certificateStream.CopyTo(memoryStream);
                var buffer = memoryStream.ToArray().AsBuffer();

                var certificate = new Certificate(buffer);
                CertificateStores.TrustedRootCertificationAuthorities.Add(certificate);
            }
        }
    }
}