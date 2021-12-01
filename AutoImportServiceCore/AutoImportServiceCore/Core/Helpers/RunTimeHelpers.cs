using System;

namespace AutoImportServiceCore.Core.Helpers
{
    public class RunTimeHelpers
    {
        /// <summary>
        /// Get the timespan with the difference between now and the next run time based on the delay.
        /// </summary>
        /// <param name="delay">The delay between the runs.</param>
        /// <returns></returns>
        public static TimeSpan GetTimeTillNextRun(TimeSpan delay)
        {
            var nextDateTime = DateTime.Now.Date;

            while(nextDateTime < DateTime.Now)
                nextDateTime += delay;

            return nextDateTime - DateTime.Now;
        }
    }
}
