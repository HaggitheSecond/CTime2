using System;

namespace CTime2.Core.Services.Band
{
    public static class BandConstants
    {
        public static readonly Guid TileId = Guid.Parse("{DB5C217C-38BF-419D-B87D-2A39982459D9}");
        public static readonly short HeaderElementId = 1;
        public static readonly short CheckInElementId = 2;
        public static readonly short CheckOutElementId = 3;

        public static readonly string BackgroundTaskId = "CTime2/BackgroundTask/BandIntegration";
        public static readonly string BackgroundTaskEntryPoint = "CTime2.BandTileService.CTime2BandTileService";
    }
}