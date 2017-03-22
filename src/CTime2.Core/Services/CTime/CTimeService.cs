using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.UI;
using Windows.Web.Http;
using Caliburn.Micro;
using CTime2.Core.Common;
using CTime2.Core.Data;
using CTime2.Core.Events;
using CTime2.Core.Extensions;
using CTime2.Core.Services.ApplicationState;
using CTime2.Core.Services.CTime.RequestCache;
using CTime2.Core.Services.GeoLocation;
using CTime2.Core.Strings;
using Polly;
using UwCore.Common;
using UwCore.Services.ApplicationState;

namespace CTime2.Core.Services.CTime
{
    public class CTimeService : ICTimeService, IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLog(typeof(CTimeService));

        private readonly ICTimeRequestCache _requestCache;
        private readonly IEventAggregator _eventAggregator;
        private readonly IApplicationStateService _applicationStateService;
        private readonly IGeoLocationService _geoLocationService;

        private readonly HttpClient _client;

        public CTimeService(ICTimeRequestCache requestCache, IEventAggregator eventAggregator, IApplicationStateService applicationStateService, IGeoLocationService geoLocationService)
        {
            Guard.NotNull(requestCache, nameof(requestCache));
            Guard.NotNull(eventAggregator, nameof(eventAggregator));
            Guard.NotNull(applicationStateService, nameof(applicationStateService));
            Guard.NotNull(geoLocationService, nameof(geoLocationService));

            this._requestCache = requestCache;
            this._eventAggregator = eventAggregator;
            this._applicationStateService = applicationStateService;
            this._geoLocationService = geoLocationService;

            this._client = new HttpClient();
        }

        public async Task<User> Login(string emailAddress, string password)
        {
            try
            {
                var data = new Dictionary<string, string>
                {
                    {"Password", this.GetHashedPassword(password)},
                    {"LoginName", emailAddress},
                    {"Crypt", 1.ToString()}
                };
                var responseJson = await this.SendRequestAsync("V2/LoginV2.php", data, canBeCached:false);

                var user = responseJson?
                    .GetNamedArray("Result", new JsonArray())
                    .Select(f => f.GetObject())
                    .FirstOrDefault();

                if (user == null)
                    return null;

                return new User
                {
                    Id = user.GetString("EmployeeGUID"),
                    CompanyId = user.GetString("CompanyGUID"),
                    Email = user.GetString("LoginName"),
                    FirstName = user.GetString("EmployeeFirstName"),
                    Name = user.GetString("EmployeeName"),
                    ImageAsPng = user.GetBase64ByteArray("EmployeePhoto"),
                    SupportsGeoLocation = user.GetInt("GeolocationAllowed") == 1,
                    CompanyImageAsPng = user.GetBase64ByteArray("CompanyImage"),
                };
            }
            catch (Exception exception)
            {
                Logger.Warn($"Exception in method {nameof(this.Login)}. Email address: {emailAddress}");
                Logger.Error(exception);

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
                    .GetNamedArray("Result", new JsonArray())
                    .Select(f => f.GetObject())
                    .Select(f => new Time
                    {
                        Day = f.GetDateTime("DayDate"),
                        Hours = f.GetTimeSpan("TimeHour_IST_HR"),
                        State = f.GetNullableEnum<TimeState>("TimeTrackTypePure"),
                        ClockInTime = f.GetNullableDateTime("TimeTrackIn"),
                        ClockOutTime = f.GetNullableDateTime("TimeTrackOut"),
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
                Logger.Warn($"Exception in method {nameof(this.GetTimes)}. Employee: {employeeGuid}, Start: {start}, End: {end}");
                Logger.Error(exception);

                throw new CTimeException(CTime2CoreResources.Get("CTimeService.ErrorWhileLoadingTimes"), exception);
            }
        }

        public async Task SaveTimer(string employeeGuid, string rfidKey, DateTime time, string companyId, TimeState state, bool withGeolocation)
        {
            try
            {
                Geopoint location = withGeolocation
                    ? await this._geoLocationService.TryGetGeoLocationAsync()
                    : null;

                var data = new Dictionary<string, string>
                {
                    {"TimerKind", ((int) state).ToString()},
                    {"TimerText", string.Empty},
                    {"TimerTime", time.ToString("yyyy-MM-dd HH:mm:ss")},
                    {"EmployeeGUID", employeeGuid},
                    {"GUID", companyId},
                    {"RFID", rfidKey},
                    {"lat", location?.Position.Latitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty },
                    {"long", location?.Position.Longitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty }
                };

                var responseJson = await this.SendRequestAsync("V2/SaveTimerV2.php", data, canBeCached:false);
                if (responseJson?.GetInt("State") == 0)
                {
                    this._eventAggregator.PublishOnCurrentThread(new UserStamped());

                    this._requestCache.Clear();
                }
            }
            catch (Exception exception)
            {
                Logger.Warn($"Exception in method {nameof(this.SaveTimer)}. Employee: {employeeGuid}, Time: {time}, Company Id: {companyId}, State: {(int)state}");
                Logger.Error(exception);

                throw new CTimeException(CTime2CoreResources.Get("CTimeService.ErrorWhileStamp"), exception);
            }
        }

        public async Task<Time> GetCurrentTime(string employeeGuid)
        {
            try
            {
                IList<Time> timesForToday = await this.GetTimes(employeeGuid, DateTime.Today.AddDays(-1), DateTime.Today);

                return timesForToday
                    .OrderByDescending(f => f.ClockInTime)
                    .FirstOrDefault();
            }
            catch (Exception exception)
            {
                Logger.Warn($"Exception in method {nameof(this.GetCurrentTime)}. Employee: {employeeGuid}");
                Logger.Error(exception);

                throw new CTimeException(CTime2CoreResources.Get("CTimeService.ErrorWhileLoadingCurrentTime"), exception);
            }
        }

        public async Task<IList<AttendingUser>> GetAttendingUsers(string companyId, byte[] defaultImage)
        {
            try
            {
                var cacheEtag = this._applicationStateService.GetAttendanceListImageCacheEtag();

                var responseJson = await this.SendRequestAsync("V2/GetPresenceListV2.php", new Dictionary<string, string>
                {
                    {"GUID", companyId},
                    {"cacheDate", cacheEtag ?? string.Empty }
                });

                if (responseJson == null)
                    return new List<AttendingUser>();
                
                var defaultImageAsBase64 = Convert.ToBase64String(defaultImage ?? new byte[0]);

                var newCacheEtag = responseJson
                    .GetNamedArray("Result", new JsonArray())
                    .Select(f => f.GetObject())
                    .Select(f => f.GetString("cacheDate"))
                    .FirstOrDefault();
                
                this._applicationStateService.SetAttendanceListImageCacheEtag(newCacheEtag);

                var result = responseJson
                    .GetNamedArray("Result", new JsonArray())
                    .Select(f => f.GetObject())
                    .Select(f => new
                    {
                        EmployeeI3D = f.GetInt("EmployeeI3D"),
                        Employee = new AttendingUser
                        {
                            Id = f.GetInt("EmployeeI3D").ToString(),
                            Name = f.GetString("EmployeeName"),
                            FirstName = f.GetString("EmployeeFirstName"),
                            AttendanceState = new AttendanceState
                            {
                                IsAttending = f.GetInt("PresenceStatus") == 1,
                                Name = this.ParseAttendanceStateName(f.GetString("TimerTypeDescription"), f.GetNullableInt("TimeTrackTypePure"), f.GetInt("PresenceStatus") == 1),
                                Color = this.ParseColor(f.GetString("EnumColor"), f.GetNullableInt("TimeTrackTypePure")),
                            },
                            ImageAsPng = Convert.FromBase64String(f.GetString("EmployeePhoto") ?? defaultImageAsBase64),
                            EmailAddress = f.GetString("EmployeeEmail"),
                            PhoneNumber = f.GetString("EmployeePhone"),
                        }
                    })
                    .GroupBy(f => f.EmployeeI3D)
                    .ToDictionary(f => f.Key, f => f.Select(d => d.Employee).FirstOrDefault());

                var imageCache = new EmployeeImageCache();
                if (newCacheEtag == cacheEtag)
                {
                    await imageCache.FillWithCachedImages(result);
                }

                if (newCacheEtag != cacheEtag)
                {
                    await imageCache.CacheImagesAsync(result);
                }

                return result.Select(f => f.Value).ToList();
            }
            catch (Exception exception)
            {
                Logger.Warn($"Exception in method {nameof(this.GetAttendingUsers)}. Company Id: {companyId}");
                Logger.Error(exception);

                throw new CTimeException(CTime2CoreResources.Get("CTimeService.ErrorWhileLoadingAttendanceList"), exception);
            }
        }

        public void Dispose()
        {
            this._client.Dispose();
        }

        private string ParseAttendanceStateName(string potentialName, int? state, bool attending)
        {
            if (state == (int)TimeState.Entered)
                return CTime2CoreResources.Get("Entered");

            if (state == (int)TimeState.Left || state == null)
                return CTime2CoreResources.Get("Left");

            if (state == (int)TimeState.HomeOffice)
                potentialName = CTime2CoreResources.Get("HomeOffice");

            if (state == (int)TimeState.ShortBreak)
                potentialName = CTime2CoreResources.Get("ShortBreak");

            if (state == (int)TimeState.Trip)
                potentialName = CTime2CoreResources.Get("Trip");

            string suffix = attending 
                ? CTime2CoreResources.Get("Entered") 
                : CTime2CoreResources.Get("Left");

            return $"{potentialName.MakeFirstCharacterUpperCase()} ({suffix})";
        }

        private Color ParseColor(string color, int? state)
        {
            //For default Entered and Left we use our own red and green colors "CTimeGreen" and "CTimeRed"
            if (state == (int) TimeState.Entered)
                return Color.FromArgb(255, 63, 195, 128);

            if (state == (int) TimeState.Left || state == null)
                return Color.FromArgb(255, 231, 76, 60);

            if (string.IsNullOrWhiteSpace(color))
                return Colors.Transparent;

            string r = color.Substring(1, 2);
            string g = color.Substring(3, 2);
            string b = color.Substring(5, 2);

            return Color.FromArgb(
                255, 
                byte.Parse(r, NumberStyles.HexNumber), 
                byte.Parse(g, NumberStyles.HexNumber), 
                byte.Parse(b, NumberStyles.HexNumber));
        }
        
        private string GetHashedPassword(string password)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var hashedPasswordBytes = MD5.Create().ComputeHash(passwordBytes);
            var hashedPasswordString = BitConverter.ToString(hashedPasswordBytes);

            return hashedPasswordString.Replace("-", string.Empty).ToLower();
        }

        private async Task<JsonObject> SendRequestAsync(string function, Dictionary<string, string> data, bool canBeCached = true)
        {
            string responseContentAsString;

            if (canBeCached == false || this._requestCache.TryGetCached(function, data, out responseContentAsString) == false)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, this.BuildUri(function))
                {
                    Content = new HttpFormUrlEncodedContent(data)
                };

                var retryPolicy = Policy
                    .Handle<Exception>()
                    .OrResult<HttpResponseMessage>(f => f.StatusCode != HttpStatusCode.Ok)
                    .WaitAndRetryAsync(5, _ => TimeSpan.FromSeconds(1));

                var response = await retryPolicy.ExecuteAsync(token => this._client.SendRequestAsync(request).AsTask(token), CancellationToken.None, continueOnCapturedContext:true);
                
                if (response.StatusCode != HttpStatusCode.Ok)
                    return null;

                responseContentAsString = await response.Content.ReadAsStringAsync();

                this._requestCache.Cache(function, data, responseContentAsString);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(0.1));
            }

            var responseJson = JsonObject.Parse(responseContentAsString);
            var responseState = (int) responseJson.GetNamedNumber("State", 0);

            if (responseState != 0)
                return null;

            return responseJson;
        }

        private Uri BuildUri(string path)
        {
            return new Uri($"https://app.c-time.net/php/{path}");
        }

        #region Internal
        private class EmployeeImageCache
        {
            public async Task FillWithCachedImages(Dictionary<int, AttendingUser> users)
            {
                foreach (var user in users)
                {
                    var cachedImage = await this.GetCachedImageAsync(user.Key);

                    if (cachedImage != null && cachedImage.Length > 0)
                        user.Value.ImageAsPng = cachedImage;
                }
            }

            private async Task<byte[]> GetCachedImageAsync(int employeeI3D)
            {
                var imageFileName = this.GetImageName(employeeI3D);
                var imagesFolder = await this.GetImagesFolderAsync();

                if (await imagesFolder.TryGetItemAsync(imageFileName) == null)
                    return null;

                var imageFile = await imagesFolder.GetFileAsync(imageFileName);

                using (var imageStream = await imageFile.OpenStreamForReadAsync())
                using (var memoryStream = new MemoryStream())
                {
                    await imageStream.CopyToAsync(memoryStream);

                    return memoryStream.ToArray();
                }
            }

            public async Task CacheImagesAsync(Dictionary<int, AttendingUser> users)
            {
                foreach (var user in users)
                {
                    await this.CacheImageAsync(user.Key, user.Value.ImageAsPng);
                }
            }

            private async Task CacheImageAsync(int employeeI3D, byte[] image)
            {
                var imageFileName = this.GetImageName(employeeI3D);
                var imagesFolder = await this.GetImagesFolderAsync();
                
                var imageFile = await imagesFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);
                using (var imageStream = await imageFile.OpenStreamForWriteAsync())
                {
                    await imageStream.WriteAsync(image, 0, image.Length);
                }
            }

            private string GetImageName(int employeeI3D)
            {
                return $"AttendingUser-{employeeI3D}.png";
            }

            private async Task<StorageFolder> GetImagesFolderAsync()
            {
                return await ApplicationData.Current.LocalFolder.CreateFolderAsync("AttendingUserImages", CreationCollisionOption.OpenIfExists);
            }
        }
        #endregion
    }
}