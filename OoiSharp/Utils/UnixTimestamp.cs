using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OoiSharp.Utils
{
    public static class UnixTimestamp
    {
        public static readonly DateTimeOffset Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public static DateTimeOffset MillisecondTimestampToDateTimeOffset(double ts)
        {
            return Epoch.AddMilliseconds(ts);
        }

        public static DateTimeOffset MillisecondTimestampToDateTimeOffset(long ts)
        {
            return Epoch.AddMilliseconds(ts);
        }

        public static long CurrentMillisecondTimestamp => (long)(DateTimeOffset.UtcNow - Epoch).TotalMilliseconds;
    }
}