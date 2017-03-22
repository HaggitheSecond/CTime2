using System.Collections.Generic;

namespace CTime2.Core.Services.CTime.RequestCache
{
    public class NullCTimeRequestCache : ICTimeRequestCache
    {
        public void Cache(string function, Dictionary<string, string> data, string response)
        {
            
        }

        public bool TryGetCached(string function, Dictionary<string, string> data, out string response)
        {
            response = null;
            return false;
        }

        public void Clear()
        {
        }
    }
}