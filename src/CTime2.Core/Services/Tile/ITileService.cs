using System;
using System.Threading.Tasks;

namespace CTime2.Core.Services.Tile
{
    public interface ITileService
    {
        DateTime StartDateForStatistics { get; set; }
        DateTime EndDateForStatistics { get; set; }
        bool IncludeTodayForStatistics { get; set; }

        Task UpdateLiveTileAsync();
    }
}